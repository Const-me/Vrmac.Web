using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace AcmeV2.Json;

/// <summary>Certificate request</summary>
sealed record class CertificateRequest: iSerialise
{
	[JsonInclude]
	public readonly string csr;
	public CertificateRequest() => csr = string.Empty;
	public CertificateRequest( byte[] bytes ) => csr = WebEncoders.Base64UrlEncode( bytes );
	public string json() => JsonSerializer.Serialize( this, Serialise.Default.CertificateRequest );
}

/// <summary>JSON response for certificate request</summary>
sealed class CertificateResponse
{
	[JsonInclude]
	public eStatus status;

	[JsonInclude]
	public string? expires, finalize, certificate;

	[JsonInclude]
	public Problem? error;

	[JsonInclude]
	public OrderIdentifier[] identifiers = Array.Empty<OrderIdentifier>();
}