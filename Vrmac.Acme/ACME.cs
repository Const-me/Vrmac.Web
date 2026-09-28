using System.Buffers.Binary;
// using System.Formats.Asn1;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace AcmeV2;

/// <summary>Public functions exported from this DLL</summary>
public static class ACME
{
	/// <summary>Find all these HTTPS endpoints, and create the client object</summary>
	/// <remarks>Correct directory for let’s encrypt:<br/><c>https://acme-v02.api.letsencrypt.org/directory</c></remarks>
	public static Task<iAcmeClient> create( string directory, CancellationToken cancel ) =>
		AcmeClient.create( directory, cancel );

	/// <summary>Generate ECDSA key pair for a new certificate</summary>
	/// <remarks>The result contains both public and private keys.<br/>
	/// It uses NIST P384 curve because at the time of writing it’s the strongest one let’s encrypt supports.</remarks>
	public static ECDsa generateCertificateKey() =>
		ECDsa.Create( ECCurve.NamedCurves.nistP384 );

	/// <summary>Validate ownership of your domain[s]. May take a while to complete.</summary>
	/// <remarks>Before calling this method, make sure to serve the magic tokens from <see cref="OrderChallenges.challenges" />
	/// at the <c>/.well-known/acme-challenge/{token}</c></remarks>
	public static Task validateChallenges( this iAcmeAccount acc, in OrderChallenges order, TimeSpan timeout )
	{
		string[] urls = order.challenges.Select( e => e.url ).ToArray();
		return acc.validateChallenges( urls, timeout );
	}

	/// <summary>Issue the new certificate.</summary>
	/// <remarks>Before calling this method, make sure <see cref="validateChallenges"/> completed successfully.<br/>
	/// The ECDSA object must contain private key for the sertificate: this method uses it to sign stuff.</remarks>
	public static Task<OrderStatus> issueCertificate( this iAcmeAccount acc, in OrderChallenges order, ECDsa key, CancellationToken cancel )
	{
		string[] domains = order.challenges.Select( i => i.domain ).ToArray();
		return acc.issueCertificate( order.finalize, order.location, domains, key, cancel );
	}

#if false
	static IEnumerable<string> domains( X509Certificate2 cert )
	{
		// Try SAN extension first
		var sanExt = cert.Extensions[ "2.5.29.17" ]; // Subject Alternative Name OID
		if( sanExt != null )
		{
			var reader = new AsnReader( sanExt.RawData, AsnEncodingRules.DER );
			var seq = reader.ReadSequence();
			while( seq.HasData )
			{
				var tag = seq.PeekTag();
				if( tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 2 ) // DNSName
				{
					string name = seq.ReadCharacterString( UniversalTagNumber.IA5String, tag );
					yield return name;
				}
				else
				{
					// Skip this element
					seq.ReadEncodedValue();
				}
			}
			yield break; // SAN exists → use only SAN
		}

		// Fallback to CN
		string cn = cert.GetNameInfo( X509NameType.DnsName, false );
		if( !string.IsNullOrWhiteSpace( cn ) )
			yield return cn;
	}
#endif

	/// <summary>Load certificate from custom binary container, and initialise with private key</summary>
	/// <remarks>The binary container is produced by <see cref="iAcmeClient.downloadCertificate" /> method.<br/>
	/// The ECDSA object should contain private key for the sertificate.<br/>
	/// It must be the same key passed to <see cref="iAcmeAccount.issueCertificate" /> method.</remarks>
	public static Certificates loadCertificates( string path, ECDsa dsa )
	{
		ReadOnlySpan<byte> span = File.ReadAllBytes( path );

		ReadOnlySpan<uint> fileHeader = MemoryMarshal.Cast<byte, uint>( span.Slice( 0, 8 ) );
		span = span.Slice( 8 );

		if( fileHeader[ 0 ] != AcmeUtils.certMagic )
			throw new ArgumentException();
		DateTime exp = AcmeUtils.fromDays( fileHeader[ 1 ] );

		int count = BinaryPrimitives.ReadUInt16LittleEndian( span );
		ReadOnlySpan<ushort> lengths = MemoryMarshal.Cast<byte, ushort>( span.Slice( 2, count * 2 ) );
		span = span.Slice( count * 2 + 2 );

		X509Certificate2[] certs = new X509Certificate2[ count ];
		for( int i = 0; i < certs.Length; i++ )
		{
			int cb = lengths[ i ];
			ReadOnlySpan<byte> source = span.Slice( 0, cb );
			span = span.Slice( cb );

			X509Certificate2 cert = X509CertificateLoader.LoadCertificate( source );
			if( 0 == i )
				cert = cert.CopyWithPrivateKey( dsa );
			certs[ i ] = cert;
		}

		return new Certificates( exp, certs );
	}

	/// <summary>Atomically write or overwrite the destination file, making sure the new data is flushed to disk.</summary>
	public static void writeBytesForReal( string path, ReadOnlySpan<byte> bytes ) =>
		AcmeClient.writeBytesForReal( path, bytes );
}