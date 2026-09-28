using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace AcmeV2.Json;

/// <summary>ECC public key used in JWS</summary>
sealed record class Key
{
	[JsonInclude]
	public readonly string kty, crv, x, y;

	/// <summary>Parameters of the key generated using NamedCurves.nistP384</summary>
	public Key( byte[] x, byte[] y )
	{
		kty = "EC";
		crv = "P-384";
		this.x = WebEncoders.Base64UrlEncode( x );
		this.y = WebEncoders.Base64UrlEncode( y );
	}

	internal static Key create( ECDsa dsa )
	{
		ECParameters ec = dsa.ExportParameters( false );
		Debug.Assert( ec.Curve.IsNamed );
		return new Key( ec.Q.X!, ec.Q.Y! );
	}
}

sealed record class NewAccountHeader: iSerialise
{
	[JsonInclude]
	public readonly string alg;

	[JsonInclude]
	public readonly Key jwk;

	[JsonInclude]
	public readonly string nonce;

	[JsonInclude]
	public readonly string url;

	public NewAccountHeader( Key jwk, string nonce, string url )
	{
		alg = "ES384";
		this.jwk = jwk;
		this.nonce = nonce;
		this.url = url;
	}

	public string json() => JsonSerializer.Serialize( this, Serialise.Default.NewAccountHeader );
}

sealed class NewAccountPayload: iSerialise
{
	[JsonInclude]
	public string[] contact;

	[JsonInclude]
	public bool termsOfServiceAgreed;

	public NewAccountPayload( string[] contact )
	{
		this.contact = contact;
		termsOfServiceAgreed = true;
	}

	public string json() => JsonSerializer.Serialize( this, Serialise.Default.NewAccountPayload );
}

sealed class NewAccountResponse
{
	[JsonInclude]
	public eStatus status;
	[JsonInclude]
	public string? orders;

	public NewAccountResponse()
	{
		status = default;
		orders = null;
	}
}