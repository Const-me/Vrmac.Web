using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
namespace Vrmac.Uploads;

static class UploaderUtils
{
	public static Task complete( this HttpContext context, HttpStatusCode code )
	{
		HttpResponse response = context.Response;
		response.StatusCode = (int)code;
		return response.CompleteAsync();
	}

	public static Task completeBadRequest( this HttpContext context ) =>
		context.complete( HttpStatusCode.BadRequest );

	/// <summary>Read bytes [ 32 .. 32+15 ] as the GUID</summary>
	public static Guid readMessageId( this ReadResult rr )
	{
		Debug.Assert( rr.Buffer.Length >= 32 + 16 );
		Span<byte> bytes = stackalloc byte[ 16 ];
		rr.Buffer.Slice( 32, 16 ).CopyTo( bytes );
		return new Guid( bytes );
	}

	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public static bool verifyHash( ReadOnlySpan<byte> span )
	{
		if( span.Length <= 32 )
			return false;

		Span<byte> correctHash = stackalloc byte[ 32 ];
		SHA256.HashData( span.Slice( 32 ), correctHash );
		return correctHash.SequenceEqual( span.Slice( 0, 32 ) );
	}

	/// <summary>Read SHA-512 of the full payload, encode into the file name</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public static string chunkRequestName( this ReadResult rr )
	{
		Span<byte> sha512 = stackalloc byte[ 64 ];
		rr.Buffer.Slice( 32 + 16, 64 ).CopyTo( sha512 );
		return Base64Url.EncodeToString( sha512 );
	}

	public static void logFail( this iUploaderHost host, string what, Exception ex, IPAddress ip )
	{
		ReadOnlySpan<char> message = ex.Message;
		host.logMessage( what, ex.HResult, message, ip );
	}

	/// <summary>Remote IP of HTTP or HTTPS connection</summary>
	public static IPAddress remoteIp( this HttpContext context )
	{
		IPAddress ip = context.Connection.RemoteIpAddress ?? throw new ApplicationException( "The server only supports TCP transport" );
		if( ip.IsIPv4MappedToIPv6 )
			ip = ip.MapToIPv4();
		return ip;
	}

	/// <summary>Send response to the client</summary>
	public static ValueTask<FlushResult> sendResponse( this HttpContext context,
		RentedArray arr )
	{
		HttpResponse response = context.Response;
		response.StatusCode = (int)arr.statusCode;
		// Disable caching everywhere
		response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
		response.Headers.Pragma = "no-cache";
		response.Headers.Expires = "0";

		response.ContentLength = arr.length;
		if( arr.length <= 0 )
			return ValueTask.FromResult( new FlushResult( isCanceled: false, isCompleted: true ) );

		response.ContentType = "application/octet-stream";
		return response.BodyWriter.WriteAsync( arr.memory );
	}

	// Defaults to 240 bytes/second with a 5 second grace period.
	// 5 seconds is fine I guess, 240 bytes/second way too slow, using 16 kbps instead.
	static readonly MinDataRate minRequestDataRate = new MinDataRate( 1 << 14, TimeSpan.FromSeconds( 5 ) );

	public static void allowLargeRequests( HttpContext context )
	{
		IHttpMaxRequestBodySizeFeature? maxSizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
		const int maxRequest = UploaderProto.maxChunk + 1024 * 4;
		if( maxSizeFeature is not null )
			maxSizeFeature.MaxRequestBodySize = maxRequest;

		IHttpMinRequestBodyDataRateFeature? minRateFeature = context.Features.Get<IHttpMinRequestBodyDataRateFeature>();
		if( minRateFeature is not null )
			minRateFeature.MinDataRate = minRequestDataRate;
	}

	public static bool isSharingViolation( this IOException ex )
	{
		const int EAGAIN = 11;
		if( ex.HResult == EAGAIN )
			return true;
		ReadOnlySpan<char> span = ex.Message;
		return span.IndexOf( "being used by another process" ) > 0;
	}
}