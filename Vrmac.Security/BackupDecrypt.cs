using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Vrmac.Security;

/// <summary>Functions to decrypt the backups</summary>
public static class BackupDecrypt
{
	/// <summary>Decrypt and import the private key</summary>
	public static ECDiffieHellman importKey( byte[] encryptedKey, string password )
	{
		using SecretBytes privateKey = SymmetricCrypto.decrypt( encryptedKey, password );
		ECDiffieHellman ecdh = ECDiffieHellman.Create( BackupKeyPair.curve );
		try
		{
			ecdh.ImportECPrivateKey( privateKey, out _ );
			return ecdh;
		}
		catch
		{
			ecdh.Dispose();
			throw;
		}
	}

	/// <summary>Decrypt backup into a file on disk</summary>
	public static void decrypt( string pathResult, string pathSource, ECDiffieHellman key, in Guid formatVersion )
	{
		using FileStream source = File.OpenRead( pathSource );

		string pathTemp = Path.ChangeExtension( pathResult, ".tmp" );
		try
		{
			using( FileStream destination = new FileStream( pathTemp, FileMode.Create, FileAccess.ReadWrite ) )
				decrypt( destination, source, key, formatVersion );
			File.Move( pathTemp, pathResult, true );
		}
		finally
		{
			if( File.Exists( pathTemp ) )
				File.Delete( pathTemp );
		}
	}

	/// <summary>Decrypts backup from stream into a destination stream</summary>
	public static void decrypt( Stream dest, Stream source, ECDiffieHellman key, in Guid formatVersion )
	{
		if( !source.CanRead )
			throw new ArgumentException( "The input stream is not readable" );
		dest.Seek( 0, SeekOrigin.Begin );
		dest.SetLength( 0 );

		Span<byte> header = stackalloc byte[ 16 + 64 + 2 + 2 ];
		source.ReadExactly( header );

		if( new Guid( header.Slice( 0, 16 ) ) != formatVersion )
			throw new ArgumentException( "Unexpected format marker" );

		byte[] checksum = header.Slice( 16, 64 ).ToArray();
		header = header.Slice( 16 + 64 );
		byte[] backupPublicKey = new byte[ BinaryPrimitives.ReadUInt16LittleEndian( header ) ];
		byte[] iv = new byte[ BinaryPrimitives.ReadUInt16LittleEndian( header.Slice( 2 ) ) ];
		source.ReadExactly( backupPublicKey );
		source.ReadExactly( iv );

		using ECDiffieHellman session = ECDiffieHellman.Create( ECCurve.NamedCurves.nistP521 );
		session.ImportSubjectPublicKeyInfo( backupPublicKey, out _ );
		using Aes aes = Aes.Create();
		byte[] symmetricKey = key.DeriveKeyMaterial( session.PublicKey );
		try
		{
			aes.KeySize = 256;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			// Reduce symmetric key into the 32 bytes we need for AES
			aes.Key = SHA256.HashData( symmetricKey );
			aes.IV = iv;
		}
		finally
		{
			CryptographicOperations.ZeroMemory( symmetricKey );
		}

		using( ICryptoTransform tform = aes.CreateDecryptor() )
		using( CryptoStream cryptoStream = new( source, tform, CryptoStreamMode.Read ) )
			cryptoStream.CopyTo( dest );

		dest.Seek( 0, SeekOrigin.Begin );
		byte[] plainHash = SHA512.HashData( dest );
		if( !plainHash.SequenceEqual( checksum ) )
			throw new ArgumentException( "SHA-512 checksum doesn't match" );

		dest.Seek( 0, SeekOrigin.Begin );
	}
}