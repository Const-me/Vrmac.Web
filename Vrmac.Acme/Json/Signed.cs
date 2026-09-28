using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
namespace AcmeV2.Json;

enum eSignedHeaderFlavour: byte
{
	EmptyJson,
	EmptyString,
}

sealed class Signed
{
	[JsonInclude]
	public string? @protected, payload, signature;

	static string base64( string json )
	{
		byte[] arr = Encoding.UTF8.GetBytes( json );
		return WebEncoders.Base64UrlEncode( arr );
	}

	static byte[] combinePayload( string header, string body )
	{
		byte[] bytes = new byte[ header.Length + 1 + body.Length ];
		Span<byte> span = bytes;
		var enc = Encoding.ASCII;
		enc.GetBytes( header, bytes );
		bytes[ header.Length ] = (byte)'.';
		enc.GetBytes( body, span.Slice( header.Length + 1 ) );
		return bytes;
	}

	public static Signed sign( ECDsa dsa, string @protected, string payload )
	{
		Debug.Assert( @protected.Contains( "ES384" ) );

		Signed json = new();
		json.@protected = base64( @protected );
		json.payload = base64( payload );

		// Sign
		byte[] bytes = combinePayload( json.@protected, json.payload );
		bytes = dsa.SignData( bytes, HashAlgorithmName.SHA384 );
		json.signature = WebEncoders.Base64UrlEncode( bytes );

		return json;
	}

	public static Signed sign<Header, Body>( ECDsa dsa, Header header, Body body )
		where Header : class, iSerialise
		where Body : class, iSerialise
	{
		return sign( dsa, header.json(), body.json() );
	}

	public static Signed signHeader<Header>( ECDsa dsa, Header header, eSignedHeaderFlavour flavour )
		where Header : class, iSerialise
	{
		string body;
		switch( flavour )
		{
			// Empty string while polling for challenges - the server does absolutely nothing,
			// returns "pending" validation status forever
			case eSignedHeaderFlavour.EmptyJson:
				body = "{}";
				break;

			// Empty JSON when requesting a certificate - the server return failed status:
			// status = 400, type = "urn:ietf:params:acme:error:malformed",
			// detail = "Unable to validate JWS :: POST-as-GET requests must have an empty payload"
			case eSignedHeaderFlavour.EmptyString:
				body = string.Empty;
				break;

			default:
				throw new ArgumentException();
		}

		return sign( dsa, header.json(), body );
	}
}