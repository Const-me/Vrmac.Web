using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
namespace AcmeV2;

sealed class AcmeClient: iAcmeClient
{
	readonly AcmeWebClient client;

	AcmeClient( AcmeWebClient client ) => this.client = client;

	async Task<AccountInfo> iAcmeClient.register( string[] contact, CancellationToken cancel )
	{
		byte[] keyBytes = AccountInfo.generateNewKey();
		using var dsa = AccountInfo.dsa( keyBytes );

		string nonce = await client.getNewNonce( cancel );

		var key = Json.Key.create( dsa );
		var header = new Json.NewAccountHeader( key, nonce, client.endpoints.newAccount );
		var payload = new Json.NewAccountPayload( contact );
		var signed = Json.Signed.sign( dsa, header, payload );

		NewAccount res = await client.newAccount( signed, cancel );
		return new AccountInfo( keyBytes, res.id );
	}

	iAcmeAccount iAcmeClient.account( AccountInfo info ) =>
		new AcmeAccount( client, info );

	/// <summary>Load certificates, and reorder them moving the actual one to the start of the list, intermediate[s] afterwards</summary>
	static List<X509Certificate2> loadCertificates( byte[] bytes, ECParameters certPublic )
	{
		// Parse the collection of certificated we just downloaded from ACME server
		string pemText = Encoding.ASCII.GetString( bytes );
		X509Certificate2Collection certs = new();
		certs.ImportFromPem( pemText );

		// Ensure the collection has exactly one certificate where the P384 public key matches the one we used to generate them
		// Move that certificate to the start of the collection
		int count = certs.Count;
		List<X509Certificate2> list = new( count );
		bool foundMatch = false;
		for( int i = 0; i < count; i++ )
		{
			X509Certificate2 cert = certs[ i ];
			ECParameters ecpCert;
			using( ECDsa? pub = cert.GetECDsaPublicKey() )
			{
				if( null == pub )
				{
					// No ECDSA public key, might be prehistoric RSA; append to the end of the list
					list.Add( cert );
					continue;
				}
				ecpCert = pub.ExportParameters( false );
			}

			if( !equalKey( ecpCert, certPublic ) )
			{
				// Public key is different, it's a chain - append to the end of the list
				list.Add( cert );
				continue;
			}

			// Found the actual certificate for the domain[s]
			if( foundMatch )
				throw new ApplicationException( "Multiple certificates in the container match the public key" );
			foundMatch = true;
			// Insert to the start of the list
			list.Insert( 0, cert );
		}
		if( !foundMatch )
			throw new ApplicationException( "No certificate matches the public key" );
		return list;
	}

	static bool equalKey( ECParameters a, ECParameters b )
	{
		if( a.Curve.Oid.Value != b.Curve.Oid.Value )
			return false;

		byte[] ax = a.Q.X!;
		byte[] bx = b.Q.X!;
		if( !equal( ax, bx ) )
			return false;

		byte[] ay = a.Q.Y!;
		byte[] by = b.Q.Y!;
		if( !equal( ay, by ) )
			return false;

		return true;

		static bool equal( byte[] a, byte[] b ) => a.SequenceEqual( b );
	}

	async Task iAcmeClient.downloadCertificate( string savePath, OrderStatus completedOrder,
		ECParameters certPublic, CancellationToken cancel )
	{
		// Download that byte array
		if( !completedOrder.success )
			throw new ArgumentException();
		byte[] bytes = await client.get( completedOrder.location!, cancel );

		List<X509Certificate2> certs = loadCertificates( bytes, certPublic );

		// Reshape into a custom binary container with expiration date in the header

		// Figure out the earliest expiration date across these certificates
		uint exp = certs[ 0 ].NotAfter.dateInDays();
		for( int i = 1; i < certs.Count; i++ )
			exp = Math.Min( exp, certs[ i ].NotAfter.dateInDays() );

		List<byte> list = new List<byte>();
		int cbCollectionHeader = 2 * certs.Count + 2;
		CollectionsMarshal.SetCount( list, 8 + cbCollectionHeader );

		// Export the certificates, appending to the list of bytes and keeping lengths of each
		Span<ushort> lengths = stackalloc ushort[ certs.Count ];
		for( int i = 0; i < certs.Count; i++ )
		{
			X509Certificate2 cert = certs[ i ];
			ReadOnlySpan<byte> arr = cert.Export( X509ContentType.Cert );
			lengths[ i ] = (ushort)arr.Length;
			list.AddRange( arr );
		}

		Span<byte> resultSpan = CollectionsMarshal.AsSpan( list );

		Span<uint> fileHeader = MemoryMarshal.Cast<byte, uint>( resultSpan.Slice( 0, 8 ) );
		fileHeader[ 0 ] = AcmeUtils.certMagic;
		fileHeader[ 1 ] = exp;

		Span<ushort> collectionHeader = MemoryMarshal.Cast<byte, ushort>( resultSpan.Slice( 8, cbCollectionHeader ) );
		collectionHeader[ 0 ] = (ushort)certs.Count;
		lengths.CopyTo( collectionHeader.Slice( 1 ) );

		writeBytesForReal( savePath, resultSpan );
	}

	public static void writeBytesForReal( string path, ReadOnlySpan<byte> bytes )
	{
		string temp = Path.ChangeExtension( path, ".tmp" );
		using( FileStream stream = File.Create( temp ) )
		{
			stream.Write( bytes );
			stream.Flush( true );
		}
		File.Move( temp, path, true );
	}

	void IDisposable.Dispose() => client.Dispose();

	public static async Task<iAcmeClient> create( string directory, CancellationToken cancel )
	{
		AcmeWebClient client = await AcmeWebClient.create( directory, cancel );
		return new AcmeClient( client );
	}
}