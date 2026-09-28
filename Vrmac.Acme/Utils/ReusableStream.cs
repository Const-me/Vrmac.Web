// The following macro should stay commented out in production builds
// #define DBG_SAVE_REQUESTS
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Text.Json;
namespace AcmeV2;

sealed class RequestBuffer: MemoryStream
{
	readonly MediaTypeHeaderValue contentType = new MediaTypeHeaderValue( "application/jose+json" );

	public StreamContent write( Json.Signed signed )
	{
		SetLength( 0 );
		JsonSerializer.Serialize( this, signed, Json.Serialise.Default.Signed );
		Seek( 0, SeekOrigin.Begin );

#if DBG_SAVE_REQUESTS
		File.WriteAllBytes( @"C:\Temp\2remove\Web\protected.json", WebEncoders.Base64UrlDecode( signed.@protected! ) );
		File.WriteAllBytes( @"C:\Temp\2remove\Web\payload.json", WebEncoders.Base64UrlDecode( signed.payload! ) );
		using( var file = File.Create( @"C:\Temp\2remove\Web\request.json" ) )
			CopyTo( file );
		Seek( 0, SeekOrigin.Begin );
#endif

		StreamContent content = new StreamContent( this );
		content.Headers.ContentType = contentType;
		return content;
	}

	protected override void Dispose( bool disposing ) { }
	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}