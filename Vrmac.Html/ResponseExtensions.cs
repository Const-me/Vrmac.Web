using Microsoft.AspNetCore.Http;
namespace Vrmac.Html;

/// <summary>Extension methods to send pre-rendered responses to web browsers</summary>
public static class ResponseExtensions
{
	static async ValueTask sendResponse( RentedArray arr, HttpResponse response, string contentType )
	{
		response.StatusCode = (int)arr.statusCode;

		ReadOnlyMemory<byte> memory = arr.memory;
		if( !memory.IsEmpty )
		{
			response.ContentType = contentType;
			response.ContentLength = memory.Length;
			await response.BodyWriter.WriteAsync( memory );
		}
	}

	/// <summary>Send response as <c>text/html</c> content type</summary>
	public static ValueTask sendHtml( this in RentedArray arr, HttpContext context ) =>
		sendResponse( arr, context.Response, "text/html; charset=utf-8" );

	/// <summary>Send response as an attachment with <c>application/zip</c> content type</summary>
	public static ValueTask sendZipAttachment( this in RentedArray arr, HttpContext context, string fileName )
	{
		HttpResponse response = context.Response;
		if( arr.length > 0 )
			response.Headers[ "Content-Disposition" ] = $"attachment; filename=\"{fileName}\"";
		return sendResponse( arr, response, "application/zip" );
	}
}