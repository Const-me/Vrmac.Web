using System.Security.Cryptography;
namespace Vrmac.Security.Symmetric;

#if SUPPORT_LEGACY_CRYPTO
/// <summary>Legacy AES CBC implementation excluded from the nuget package</summary>
static class SymmetricCBC
{
	/// <summary>Encrypt array of bytes using the binary password</summary>
	public static byte[] encrypt( byte[] data, byte[] passwordBytes )
	{
		if( passwordBytes.Length != 16 )
			throw new ArgumentException( "Expected a password made with generatePassword() function" );

		using SecretBytes salt = new( saltSize );
		using SecretBytes iv = new( ivSize );
		RandomNumberGenerator.Fill( salt.span );
		RandomNumberGenerator.Fill( iv.span );

		using SecretBytes key = createSymmetricKey( passwordBytes, salt );
		using SecretBytes ciphertext = default;
		using( Aes aes = createAes() )
		using( ICryptoTransform encryptor = aes.CreateEncryptor( key.array, iv.array ) )
			ciphertext.assign( encryptor.TransformFinalBlock( data, 0, data.Length ) );

		return Spans.concat( salt, iv, ciphertext );
	}

	/// <summary>Decrypt array of bytes using the binary password</summary>
	public static SecretBytes decrypt( byte[] data, ReadOnlySpan<byte> password )
	{
		try
		{
			ReadOnlySpan<byte> span = data;
			using SecretBytes salt = new( span.Slice( 0, saltSize ) );
			using SecretBytes iv = new( span.Slice( saltSize, ivSize ) );

			const int headerSize = saltSize + ivSize;

			using SecretBytes key = createSymmetricKey( password, salt );
			using SecretBytes plaintext = default;
			using( Aes aes = createAes() )
			using( ICryptoTransform decryptor = aes.CreateDecryptor( key.array, iv.array ) )
				plaintext.assign( decryptor.TransformFinalBlock( data, headerSize, data.Length - headerSize ) );
			return plaintext.move();
		}
		catch( Exception ex )
		{
			throw new ApplicationException( "Unable to decrypt the protected data", ex );
		}
	}

	static Aes createAes()
	{
		Aes aes = Aes.Create();
		aes.KeySize = 256;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;
		return aes;
	}

	/// <summary>Number of iterations for the password bytes generation function</summary>
	const int derivationIterations = 1024 * 1024;
	/// <summary>Count of bytes in the IV</summary>
	const int ivSize = 16;
	/// <summary>Count of salt bytes</summary>
	const int saltSize = 32;
	/// <summary>Count of symmetric key bytes to request from Rfc2898DeriveBytes.Pbkdf2 algorithm</summary>
	const int keySize = 32;

	static SecretBytes createSymmetricKey( ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt ) =>
		new( Rfc2898DeriveBytes.Pbkdf2( password, salt, derivationIterations, HashAlgorithmName.SHA256, keySize ) );
}
#endif