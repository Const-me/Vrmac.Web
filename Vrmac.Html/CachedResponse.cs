using Microsoft.AspNetCore.Http;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO.Hashing;
using System.Net;
namespace Vrmac.Html;

/// <summary>Pre-rendered HTML response cached in memory for reuse</summary>
/// <seealso cref="DynamicPageRender.renderCached" />
public sealed class CachedResponse
{
	readonly ReadOnlyMemory<byte> html;
	readonly string etag;
	/// <summary>Modification timestamp passed to constructor</summary>
	public DateTime modified { get; }
	/// <summary>Status code of the cached response</summary>
	public HttpStatusCode code { get; }

	/// <summary>Construct with a failed HTTP status code</summary>
	public CachedResponse( HttpStatusCode code, DateTime timestamp )
	{
		Debug.Assert( code != HttpStatusCode.OK );
		this.code = code;
		modified = timestamp;
		etag = computeEtag( code, modified, default );
	}

	/// <summary>Construct with HTTP 200 OK status code, and the provided HTML content</summary>
	public CachedResponse( byte[] html, DateTime timestamp )
	{
		this.html = html;
		modified = timestamp;
		code = HttpStatusCode.OK;
		etag = computeEtag( code, modified, html );
	}

	static string computeEtag( HttpStatusCode code, DateTime modified, ReadOnlySpan<byte> body )
	{
		// Compute XxHash64 of status code, modified timestamp, and payload bytes
		const long seed = 11;

		Span<byte> span = stackalloc byte[ 12 ];
		BinaryPrimitives.WriteInt64LittleEndian( span, modified.Ticks );
		BinaryPrimitives.WriteInt32LittleEndian( span.Slice( 8 ), (int)code );
		XxHash64 hash = new( seed );
		hash.Append( span );
		hash.Append( body );

		span = span.Slice( 0, 8 );
		int cb = hash.GetCurrentHash( span );
		Debug.Assert( cb == span.Length );

		// According to RFC 7232 ETags must be quoted strings
		int ccEncoded = Base64Url.GetEncodedLength( span.Length );
		Span<char> chars = stackalloc char[ ccEncoded + 2 ];
		chars[ 0 ] = '\"';
		Base64Url.EncodeToChars( span, chars.Slice( 1, ccEncoded ) );
		chars[ chars.Length - 1 ] = '\"';
		return new string( chars );
	}

	bool notModified( HttpContext context )
	{
		ReadOnlySpan<char> etag = this.etag;
		ReadOnlySpan<char> inm = context.Request.Headers.IfNoneMatch;
		return inm.SequenceEqual( etag ) || inm.SequenceEqual( etag.Slice( 1, etag.Length - 2 ) );
	}

	void setHeaders( HttpResponse response )
	{
		IHeaderDictionary headers = response.Headers;
		headers.CacheControl = DynamicPageRender.cacheControl;
		response.Headers.ETag = etag;
	}

	/// <summary>Send this cached response to the client</summary>
	/// <remarks>Depending on the request headers, the method may instead send HTTP 304 NotModified response with just the headers</remarks>
	public async Task send( HttpContext context )
	{
		HttpResponse response = context.Response;
		setHeaders( response );

		if( notModified( context ) )
		{
			response.StatusCode = (int)HttpStatusCode.NotModified;
			return;
		}

		response.StatusCode = (int)code;
		if( !html.IsEmpty )
		{
			response.ContentType = "text/html; charset=utf-8";
			response.ContentLength = html.Length;
			await response.BodyWriter.WriteAsync( html );
		}
	}
}