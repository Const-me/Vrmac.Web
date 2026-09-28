using Microsoft.AspNetCore.Http;
using System.Net;
namespace Vrmac.Html;

/// <summary>Implement this interface to provide outer pieces of the HTML shared by all pages</summary>
public interface iPageLayout
{
	/// <summary>Produce a complete HTML document with the page</summary>
	void applyTemplate<T>( Stream stream, in T page ) where T : struct, iPageBase;
}

/// <summary>Utility functions to assemble complete HTML documents from layout and content</summary>
public static class DynamicPageRender
{
	/// <summary>Render dynamic HTML page, send the response</summary>
	/// <remarks>The response will contain headers to disable caching</remarks>
	public static Task sendPage<T>( this iPageLayout layout, HttpContext context, object? state )
		where T : struct, iDynamicPage
	{
		return sendResponse( context, renderDynamic<T>( layout, context, state ) );
	}

	/// <summary>Render a rarely changing dynamic page, return response in a newly allocated array in managed memory</summary>
	/// <remarks>The response includes <c>etag</c> header to detect when web browser cache already contains latest version</remarks>
	public static CachedResponse renderCached<T>( this iPageLayout layout, DateTime modified, object? state )
		where T : struct, iStaticPage
	{
		try
		{
			T page = default;
			HttpStatusCode status = page.initialise( state );
			if( status != HttpStatusCode.OK )
				return new CachedResponse( status, modified );

			MemoryStream buffer = CachedMemStream.threadLocal();
			// The function is auto-generated from Layout.html by the custom tool
			layout.applyTemplate( buffer, page );
			return new CachedResponse( buffer.ToArray(), modified );
		}
		catch
		{
			return new CachedResponse( HttpStatusCode.InternalServerError, modified );
		}
	}

	const int defaultCacheSeconds = 32;
	internal static string cacheControl { get; private set; } = $"public, max-age={defaultCacheSeconds}";

	/// <summary>Change the frequency at which browsers poll for changes of cached pages</summary>
	/// <remarks>At the time of writing, the default is 32 seconds</remarks>
	public static void setMaxCacheAge( int seconds )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( seconds );
		cacheControl = $"public, max-age={seconds}";
	}

	static RentedArray renderDynamic<T>( iPageLayout layout, HttpContext context, object? state )
		where T : struct, iDynamicPage
	{
		try
		{
			T page = default;
			HttpStatusCode status = page.initialise( context, state );
			if( status != HttpStatusCode.OK )
				return new RentedArray( status );

			MemoryStream buffer = CachedMemStream.threadLocal();
			layout.applyTemplate( buffer, page );
			return buffer.copyBytes();
		}
		catch( FormatException )
		{
			return new RentedArray( HttpStatusCode.BadRequest );
		}
		catch
		{
			return new RentedArray( HttpStatusCode.InternalServerError );
		}
	}

	/// <summary>Send response to the client’s web browser</summary>
	/// <remarks>Implements the downstream, async half of the pipeline; note the function is single instance, no longer generic.</remarks>
	static async Task sendResponse( HttpContext context, RentedArray bytes )
	{
		context.disableResponseCache();

		// If the argument owns a rented array, return after sent
		using RentedArray raii = bytes;

		await bytes.sendHtml( context );
	}
}