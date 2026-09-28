using System.Text.Json;
using System.Text.Json.Serialization;
namespace AcmeV2.Json;

sealed record class ProtectedHeader: iSerialise
{
	[JsonInclude]
	public readonly string alg, kid, nonce, url;

	public ProtectedHeader( string kid, string nonce, string url )
	{
		alg = "ES384";
		this.kid = kid;
		this.nonce = nonce;
		this.url = url;
	}

	public string json() => JsonSerializer.Serialize( this, Serialise.Default.ProtectedHeader );
}