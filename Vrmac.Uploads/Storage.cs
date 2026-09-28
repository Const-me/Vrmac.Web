using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Vrmac.Uploads;

/// <summary>Directory on the server with uploaded files</summary>
sealed class Storage
{
	/// <summary>Folder for complete uploads</summary>
	readonly string storageRoot;
	/// <summary>Folder for incomplete uploads</summary>
	readonly string storageTemp;
	/// <summary>Lock to protect byte counters; also protects file creation and rename operations</summary>
	readonly object syncRoot = new();
	/// <summary>Injected dependency with the host</summary>
	readonly iUploaderHost host;
	readonly CancellationToken appStopping;

	/// <summary>Count of bytes used by completed files</summary>
	/// <remarks>The completed files occasionally disappear from that folder.<br/>
	/// Server admin tool has a command which uses SFTP to move them from the server.</remarks>
	long cbCompleted;
	/// <summary>Count of bytes used by temporary files</summary>
	/// <remarks>That subfolder is used exclusively by the current server process, no IPC there</remarks>
	long cbTemp;
	/// <summary>Time when cbCompleted number is considered expired and it's time to refresh</summary>
	long timeRefreshCompleted;
	readonly Guid idChunkRequest;
	/// <summary>Maximum disk space allowed for the uploaded files</summary>
	/// <remarks>Assumed to be a large number, gigabytes; 16 bytes headers in the temporary files are ignored.</remarks>
	readonly long cbQuota;
	/// <summary>Approximately 17.5 minutes</summary>
	const int msRefreshCompleted = 1024 * 1024;

	public Storage( iUploaderHost host, CancellationToken appStopping, in UploaderLimits limits )
	{
		storageRoot = host.storageRoot;
		storageTemp = Path.Combine( storageRoot, "temp" );
		Directory.CreateDirectory( storageTemp );
		this.host = host;
		idChunkRequest = host.idChunkRequest;
		this.appStopping = appStopping;

		cbQuota = limits.maxDiskSpace;
		cbCompleted = QuotaUtils.completedBytes( storageRoot );
		cbTemp = QuotaUtils.tempBytes( storageTemp );
		timeRefreshCompleted = Environment.TickCount64 + msRefreshCompleted;
	}

	/// <summary>Implements start/resume request; returns next store offset on success, HTTP status code on fail</summary>
	/// <remarks>The method assumes the disk is local NVMe, hopefully server grade i.e. low latency with many IOPS</remarks>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public OffsetOrStatus uploadOffset( string name, long length )
	{
		lock( syncRoot )
		{
			string path = Path.Combine( storageRoot, name );
			FileInfo fi = new FileInfo( path );
			if( fi.Exists )
			{
				// We have a complete upload..
				if( length == fi.Length )
					return length; // And the length is good
				return HttpStatusCode.BadRequest;
			}

			path = Path.Combine( storageTemp, name );
			fi = new FileInfo( path );

			Span<byte> span = stackalloc byte[ 16 ];
			if( !fi.Exists )
			{
				long now = Environment.TickCount64;
				if( now > timeRefreshCompleted )
				{
					// Time to refresh size of completed files. Note we are holding the lock.
					cbCompleted = QuotaUtils.completedBytes( storageRoot );
					timeRefreshCompleted = now + msRefreshCompleted;
				}

				// Reject new upload when the storage is full i.e. would exceed the quota
				if( cbCompleted + cbTemp + length > cbQuota )
					return HttpStatusCode.InsufficientStorage;

				// We don't even have a partial upload
				// Create metadata file with length from the request, zero offset, and return 0 to start from the beginning
				MemoryMarshal.Write( span, length );
				MemoryMarshal.Write( span.Slice( 8 ), (long)0 );

				string tmp = Path.Combine( storageTemp, name + ".tmp" );
				File.WriteAllBytes( tmp, span );
				File.Move( tmp, path, true );
				cbTemp += length;
				return 0;
			}
			else
			{
				// Load header in the first 16 bytes of the temporary file
				try
				{
					using FileStream fs = fi.OpenRead();
					fs.ReadExactly( span );
					// Verify length is correct
					if( length != MemoryMarshal.Read<long>( span ) )
						return HttpStatusCode.BadRequest;
					// Return completed offset so far
					return MemoryMarshal.Read<long>( span.Slice( 8 ) );
				}
				catch( IOException ex ) when( ex.isSharingViolation() )
				{
					return HttpStatusCode.Conflict;
				}
			}
		}
	}

	/// <summary>Handle one POST request with a chunk</summary>
	public async Task<HttpStatusCode> writeChunk( PipeReader reader, long requestLength,
		IPAddress ip, CancellationToken requestAborted )
	{
		try
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource( requestAborted, appStopping );
			CancellationToken cancel = cts.Token;

			ReadResult rr = await reader.ReadAtLeastAsync( UploaderProto.chunkRequestHeader, cancel );
			HttpStatusCode status;
			// On success, that method also resizes the temporary file to make space for the entire chunk
			using ChunkContext? cc = ChunkContext.create( rr, storageTemp, requestLength, out status, idChunkRequest );
			SequencePosition pos = rr.Buffer.GetPosition( UploaderProto.chunkRequestHeader );
			reader.AdvanceTo( pos, pos );
			if( cc == null )
				return status;

			// Fetch 16 kb pieces from the incoming request
			const int bufferSize = 1024 * 16;
			while( cc.bytesLeft > 0 )
			{
				int slice = Math.Min( cc.bytesLeft, bufferSize );
				rr = await reader.ReadAtLeastAsync( slice, cancel );
				ReadOnlySequence<byte> consume = rr.Buffer;
				if( consume.Length < slice )
					return HttpStatusCode.BadRequest;
				// The write method does 2 jobs: store to disk using RandomAccess.Write, and update SHA-256 IncrementalHash
				foreach( ReadOnlyMemory<byte> mem in consume )
					cc.write( mem );
				reader.AdvanceTo( consume.End, consume.End );
			}

			// On success, that method also updates current offset in the header of the temporary file
			if( !cc.verifyChunkHash() )
			{
				host.logMessage( nameof( writeChunk ), E_INVALIDARG, "SHA-256 hash fail", ip );
				return HttpStatusCode.BadRequest;
			}

			if( null == cc.completeName )
				return status;

			return handleCompleted( cc, ip );
		}
		catch( FileNotFoundException ex )
		{
			host.logFail( nameof( writeChunk ), ex, ip );
			return HttpStatusCode.NotFound;
		}
		catch( OperationCanceledException ) when( appStopping.IsCancellationRequested )
		{
			return HttpStatusCode.ServiceUnavailable;
		}
		catch( Exception ex )
		{
			host.logFail( nameof( writeChunk ), ex, ip );
			return HttpStatusCode.InternalServerError;
		}
	}

	internal static readonly int E_INVALIDARG = new ArgumentException().HResult;

	/// <summary>Handle a complete upload</summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	HttpStatusCode handleCompleted( ChunkContext cc, IPAddress ip )
	{
		long length;
		if( !cc.verifyFullHash() )
		{
			host.logMessage( nameof( handleCompleted ), E_INVALIDARG, "SHA-512 hash fail", ip );
			length = cc.deleteTemp( storageTemp );
			lock( syncRoot )
				cbTemp -= length;
			return HttpStatusCode.UnprocessableContent;
		}

		lock( syncRoot )
		{
			length = cc.moveCompleted( storageRoot, storageTemp );
			cbCompleted += length;
			cbTemp -= length;
		}
		const int S_OK = 0;
		host.logMessage( nameof( handleCompleted ), S_OK, $"Someone has uploaded a file: {length} bytes", ip );
		// The clients expect HTTP 201 Created status when upload is complete
		return HttpStatusCode.Created;
	}

	/// <summary>Erase old incomplete uploads</summary>
	/// <remarks>Should be called at regular intervals by a long running periodic task</remarks>
	public void cleanupOldTemporaries( TimeSpan maxAge )
	{
		DateTime minTimestamp = DateTime.UtcNow - maxAge;

		Span<byte> headerSpan = stackalloc byte[ 8 ];
		int deletedFiles = 0;
		lock( syncRoot )
		{
			foreach( string path in Directory.EnumerateFiles( storageTemp ) )
			{
				ReadOnlySpan<char> span = path;
				span = Path.GetExtension( span );
				if( span.SequenceEqual( ".tmp" ) )
					continue;
				if( File.GetCreationTimeUtc( path ) >= minTimestamp )
					continue;

				long length;
				try
				{
					// ChunkContext class opens them like that: new FileStream( path, FileMode.Open, FileAccess.ReadWrite, FileShare.None )
					// FileShare should skip a currently uploading file
					// MinRequestBodyDataRate makes sure it never stays open forever, uploader requires at least 16 kbps rate
					using( FileStream stream = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.None ) )
						stream.ReadExactly( headerSpan );
					length = MemoryMarshal.Read<long>( headerSpan );
					File.Delete( path );
				}
				catch( Exception ex )
				{
					host.logFail( nameof( cleanupOldTemporaries ), ex, IPAddress.Loopback );
					continue;
				}

				deletedFiles++;
				cbTemp -= length;
				Debug.Assert( cbTemp >= 0 );
				cbTemp = Math.Max( 0, cbTemp );
			}
		}

		int hr;
		string message;
		if( 0 == deletedFiles )
			(hr, message) = (1, "No old temporary files found");
		else
			(hr, message) = (0, $"Deleted {deletedFiles} temporary file[s]");
		host.logMessage( nameof( cleanupOldTemporaries ), hr, message, IPAddress.Loopback );
	}
}