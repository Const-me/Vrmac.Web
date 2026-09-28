using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Vrmac.Uploads;

/// <summary>Context for a single POST request which uploads 1 chunk of the file</summary>
/// <remarks>At the time of writing chunks are 2MB, except the last one which is typically smaller.</remarks>
sealed class ChunkContext: IDisposable
{
	readonly IncrementalHash incHash;
	readonly FileStream fileStream;
	readonly Sha256Hash requestHash;
	long offset;
	/// <summary>Count of bytes left to read from the request</summary>
	internal int bytesLeft { get; private set; }
	/// <summary>If this chunk is the final one which will complete the entire upload, file name. Otherwise null.</summary>
	internal string? completeName { get; private set; }

	/// <summary>Write bytes to disk, also hash them</summary>
	public void write( ReadOnlyMemory<byte> mem )
	{
		ReadOnlySpan<byte> span = mem.Span;
		RandomAccess.Write( fileStream.SafeFileHandle, span, offset + 16 );
		incHash.AppendData( span );
		Debug.Assert( span.Length <= bytesLeft );
		offset += span.Length;
		bytesLeft -= span.Length;
	}

	/// <summary>True when the hashes match</summary>
	public bool verifyChunkHash()
	{
		if( !requestHash.verify( incHash ) )
			return false;

		// Update offset in the second half of the 16 bytes header
		Span<byte> span = stackalloc byte[ 8 ];
		MemoryMarshal.Write( span, offset );
		RandomAccess.Write( fileStream.SafeFileHandle, span, 8 );
		return true;
	}

	/// <summary>Verify SHA-512 hash of the entire uploaded file, skiping the 16 bytes prefix</summary>
	public bool verifyFullHash()
	{
		Debug.Assert( null != completeName );
		Span<byte> expectedHash = stackalloc byte[ 64 ];
		Base64Url.DecodeFromChars( completeName, expectedHash );

		fileStream.Seek( 16, SeekOrigin.Begin );
		Span<byte> contentHash = stackalloc byte[ 64 ];
		SHA512.HashData( fileStream, contentHash );

		return expectedHash.SequenceEqual( contentHash );
	}

	/// <summary>Move completed file to the final destination, return its length</summary>
	public long moveCompleted( string storageRoot, string storageTemp )
	{
		Debug.Assert( null != completeName );
		string pathCurrent = Path.Combine( storageTemp, completeName );
		string pathTemp = Path.Combine( storageTemp, completeName + ".tmp" );
		string pathFinal = Path.Combine( storageRoot, completeName );
		long length = fileStream.Length - 16;

		try
		{
			fileStream.Seek( 16, SeekOrigin.Begin );
			using( FileStream tmp = File.Create( pathTemp ) )
				fileStream.CopyTo( tmp );
			File.Move( pathTemp, pathFinal, true );
			if( !OperatingSystem.IsLinux() )
			{
				// Linux allows to delete open files; Windows does not
				fileStream.Dispose();
			}
			File.Delete( pathCurrent );
			return length;
		}
		catch
		{
			try
			{
				if( File.Exists( pathTemp ) )
					File.Delete( pathTemp );
			}
			catch { }
			throw;
		}
	}

	public long deleteTemp( string storageTemp )
	{
		Debug.Assert( null != completeName );
		string pathCurrent = Path.Combine( storageTemp, completeName );
		long length = fileStream.Length - 16;

		if( !OperatingSystem.IsLinux() )
		{
			// Linux allows to delete open files; Windows does not
			fileStream.Dispose();
		}
		File.Delete( pathCurrent );
		return length;
	}

	/// <summary>Create the object</summary>
	/// <remarks>Will fail unless <see cref="Storage.uploadOffset" /> function created the temporary file</remarks>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public static ChunkContext? create( ReadResult rr, string storageTemp, long requestLength, out HttpStatusCode code, in Guid idChunkRequest )
	{
		if( rr.Buffer.Length < UploaderProto.chunkRequestHeader )
			return fail( out code );

		Span<byte> span = stackalloc byte[ UploaderProto.chunkRequestHeader ];
		rr.Buffer.Slice( 0, UploaderProto.chunkRequestHeader ).CopyTo( span );

		// See Readme.md "Upload chunk request" section
		if( idChunkRequest != new Guid( span.Slice( 32, 16 ) ) )
			return fail( out code );

		long storeOffset = MemoryMarshal.Read<long>( span.Slice( 32 + 16 + 64, 8 ) );
		if( storeOffset < 0 )
			return fail( out code );
		int chunkLength = MemoryMarshal.Read<int>( span.Slice( 32 + 16 + 64 + 8 ) );
		if( chunkLength <= 0 || chunkLength > UploaderProto.maxChunk )
			return fail( out code );
		if( requestLength != chunkLength + UploaderProto.chunkRequestHeader )
			return fail( out code );

		string name = Base64Url.EncodeToString( span.Slice( 32 + 16, 64 ) );
		string path = Path.Combine( storageTemp, name );
		if( !File.Exists( path ) )
			return fail( out code, HttpStatusCode.NotFound );

		FileStream stream;
		try
		{
			stream = new FileStream( path, FileMode.Open, FileAccess.ReadWrite, FileShare.None );
		}
		catch( IOException ex ) when( ex.isSharingViolation() )
		{
			code = HttpStatusCode.Conflict;
			return null;
		}
		bool success = false;
		IncrementalHash? incHash = null;
		try
		{
			Span<byte> header = stackalloc byte[ 16 ];
			stream.ReadExactly( header );

			long cbEntireFile = MemoryMarshal.Read<long>( header );
			long cbUploadedSlice = MemoryMarshal.Read<long>( header.Slice( 8 ) );
			if( storeOffset != cbUploadedSlice || storeOffset + chunkLength > cbEntireFile )
				return fail( out code );

			Sha256Hash expectedHash = Sha256Hash.create( span );
			incHash = Sha256Hash.createHash();
			// Feed request header (excluding the hash) into the SHA-256
			incHash.AppendData( span.Slice( 32 ) );

			ChunkContext obj = new ChunkContext( incHash, stream, expectedHash, storeOffset, chunkLength );
			success = true;
			// Detecting the final chunk in the file
			if( cbUploadedSlice + chunkLength < cbEntireFile )
			{
				code = HttpStatusCode.NoContent;
			}
			else
			{
				code = HttpStatusCode.Created;
				obj.completeName = name;
			}
			return obj;
		}
		finally
		{
			if( !success )
			{
				incHash?.Dispose();
				stream.Dispose();
			}
		}
	}

	ChunkContext( IncrementalHash incHash, FileStream fileStream, Sha256Hash requestHash,
		long offset, int bytesLeft )
	{
		this.incHash = incHash;
		this.fileStream = fileStream;
		this.requestHash = requestHash;
		this.offset = offset;
		this.bytesLeft = bytesLeft;

		// Allocate length of the file to accomodate random writes which will follow soon
		long fileEnd = offset + bytesLeft + 16;
		if( fileEnd > fileStream.Length )
			fileStream.SetLength( fileEnd );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	static ChunkContext? fail( out HttpStatusCode res, HttpStatusCode code = HttpStatusCode.BadRequest )
	{
		res = code;
		return null;
	}

	public void Dispose()
	{
		fileStream.Dispose();
		incHash.Dispose();
	}
}