using System.Security.Cryptography.X509Certificates;
namespace AcmeV2;

/// <summary>A container of certificates loaded from the custom binary format</summary>
public readonly struct Certificates
{
	/// <summary>Expiration date of the certificate</summary>
	/// <remarks>Need that field to figure out when it's time to renew</remarks>
	public readonly DateTime expiration;

	/// <summary>Certificates in the container.</summary>
	/// <remarks>The first one is the leaf, and is guaranteed to have the private key.</remarks>
	public readonly X509Certificate2[] certs;

	internal Certificates( DateTime expiration, X509Certificate2[] certs )
	{
		this.expiration = expiration;
		this.certs = certs;
	}
}