using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Vrmac.Uploads;

/// <summary>Implements resumable uploader</summary>
public sealed class Uploader
{
	readonly iUploaderHost host;
	readonly CancellationToken appStopping;
	readonly Guid idUploadRequest, idChunkRequest, idUploadResponse;
	readonly UploaderLimits limits;
	readonly Storage storage;
	readonly ECDsa? identityKey;
	/// <summary>Lock to protect the identity key</summary>
	readonly object syncRoot = new();

	/// <summary>Create the class</summary>
	public Uploader( iUploaderHost host, CancellationToken appStopping )
	{
		this.host = host;
		this.appStopping = appStopping;
		limits = host.limits;

		// Validate a few things
		if( limits.maxTempLifetime.Ticks < TimeSpan.TicksPerHour )
		{
			limits.maxTempLifetime = TimeSpan.FromTicks( TimeSpan.TicksPerHour );
			host.logMessage( nameof( Uploader ), Storage.E_INVALIDARG, "Misconfigured server: UploaderLimits.maxTempLifetime is too low, clamped to 1 hour", IPAddress.Loopback );
		}
		if( limits.minUploadSize < 1 )
		{
			limits.minUploadSize = 1;
			host.logMessage( nameof( Uploader ), Storage.E_INVALIDARG, "Misconfigured server: UploaderLimits.minUploadSize is too small, clamped to 1 byte", IPAddress.Loopback );
		}
		if( limits.maxUploadSize < limits.minUploadSize )
		{
			limits.maxUploadSize = limits.minUploadSize;
			host.logMessage( nameof( Uploader ), Storage.E_INVALIDARG, "Misconfigured server: UploaderLimits.maxUploadSize is smaller than minUploadSize, increased the maximum", IPAddress.Loopback );
		}

		storage = new Storage( host, appStopping, limits );
		identityKey = host.serverIdentityKey;
		idUploadRequest = host.idUploadRequest;
		idChunkRequest = host.idChunkRequest;
		idUploadResponse = host.idUploadResponse;
	}

	/// <summary>Map the only endpoint of the library</summary>
	public void mapRoute( WebApplication app, string postEndpoint ) =>
		app.MapPost( postEndpoint, handlePost );

	const int cbRequestHeaders = 32 + 16;
	async Task handlePost( HttpContext context )
	{
		UploaderUtils.allowLargeRequests( context );
		IPAddress ip = context.remoteIp();
		await host.rateLimit( ip );

		try
		{
			PipeReader reader = context.Request.BodyReader;
			ReadResult rr = await reader.ReadAtLeastAsync( cbRequestHeaders, appStopping );
			await handlePost( context, reader, rr, ip );
		}
		catch( Exception ex )
		{
			host.logFail( nameof( handlePost ), ex, ip );
			throw;
		}
	}

	Task handlePost( HttpContext context, PipeReader reader, ReadResult rr, IPAddress ip )
	{
		if( rr.Buffer.Length < cbRequestHeaders )
			return context.completeBadRequest();
		Guid id = rr.readMessageId();
		reader.AdvanceTo( rr.Buffer.Start, rr.Buffer.GetPosition( 32 + 16 ) );

		if( id == idChunkRequest )
			return handleChunk( context, reader, ip );
		if( id == idUploadRequest )
			return handleUpload( context, reader, ip );

		return context.completeBadRequest();
	}

	/// <summary>Handle upload of 1 piece of a file</summary>
	async Task handleChunk( HttpContext context, PipeReader reader, IPAddress ip )
	{
		long? length = context.Request.ContentLength;
		if( !length.HasValue )
		{
			await context.completeBadRequest();
			return;
		}

		try
		{
			HttpStatusCode status = await storage.writeChunk( reader, length.Value, ip, context.RequestAborted );
			await context.complete( status );
		}
		catch( Exception ex )
		{
			host.logFail( "handleChunk", ex, ip );
			await context.complete( HttpStatusCode.InternalServerError );
		}
	}

	const int cbUploadRequest = UploaderProto.uploadRequest;

	/// <summary>Handle start or resume of an upload</summary>
	async Task handleUpload( HttpContext context, PipeReader reader, IPAddress ip )
	{
		long? length = context.Request.ContentLength;
		if( length != cbUploadRequest )
		{
			await context.completeBadRequest();
			return;
		}

		ReadResult rr = await reader.ReadAtLeastAsync( cbUploadRequest, appStopping );
		using RentedArray response = handleUploadSync( rr, ip );
		reader.AdvanceTo( rr.Buffer.GetPosition( cbUploadRequest ) );

		await context.sendResponse( response );
	}

	/// <summary>Synchronous portion of start/resume handler</summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	RentedArray handleUploadSync( ReadResult rr, IPAddress ip )
	{
		if( rr.Buffer.Length < cbUploadRequest )
			return failUpload( ip );

		// The magic number is 136 bytes, small enough to stack allocate
		Span<byte> span = stackalloc byte[ cbUploadRequest ];
		rr.Buffer.Slice( 0, cbUploadRequest ).CopyTo( span );

		Debug.Assert( idUploadRequest == new Guid( span.Slice( 32, 16 ) ) );

		if( !UploaderUtils.verifyHash( span ) )
			return failUpload( ip );

		long length = BitConverter.ToInt64( span.Slice( 128 ) );
		if( length < limits.minUploadSize || length > limits.maxUploadSize )
			return failUpload( ip );
		string name = Base64Url.EncodeToString( span.Slice( 64, 64 ) );

		OffsetOrStatus offset = storage.uploadOffset( name, length );
		if( offset.status is HttpStatusCode statusCode )
			return failUpload( ip, statusCode );

		byte[]? signature = null;
		ushort signatureLength = 0;
		if( null != identityKey )
		{
			ReadOnlySpan<byte> nonce = span.Slice( 32 + 16, 16 );
			lock( syncRoot )
				signature = identityKey.SignData( nonce, HashAlgorithmName.SHA256 );

			if( signature.Length > ushort.MaxValue )
				return failUpload( ip, HttpStatusCode.InternalServerError );
			signatureLength = (ushort)signature.Length;
		}

		// See Readme.md for the protocol
		int cbResponse = UploaderProto.uploadResponseHeader;
		if( signatureLength != 0 )
			cbResponse += signatureLength;

		RentedArray resp = RentedArray.create( cbResponse );
		try
		{
			span = resp.span;
			idUploadResponse.TryWriteBytes( span.Slice( 32, 16 ) );
			long offsetBytes = offset.offset!.Value;
			MemoryMarshal.Write( span.Slice( 32 + 16, 8 ), offsetBytes );
			MemoryMarshal.Write( span.Slice( 32 + 16 + 8, 2 ), signatureLength );
			if( 0 != signatureLength )
				signature.AsSpan().CopyTo( span.Slice( UploaderProto.uploadResponseHeader ) );

			SHA256.HashData( span.Slice( 32 ), span.Slice( 0, 32 ) );
			host.logMessage( "upload", 0, $"Upload request succeeded, offset {offsetBytes}", ip );
			return resp;
		}
		catch
		{
			resp.Dispose();
			throw;
		}
	}

	RentedArray failUpload( IPAddress ip, HttpStatusCode code = HttpStatusCode.BadRequest )
	{
		host.logMessage( "upload", Storage.E_INVALIDARG, code.ToString(), ip );
		return new RentedArray( code );
	}

	/// <summary>Cleanup old temporary files, enforcing retention policy for them</summary>
	public void cleanupOldTemporaries() => storage.cleanupOldTemporaries( limits.maxTempLifetime );
}