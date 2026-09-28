using System.Security.Cryptography;
namespace Vrmac.Security.Symmetric;

/// <summary>Utility functions to encrypt and decrypt secrets with AES-256 GCM</summary>
/// <remarks>Designed for use with high entropy 16 bytes symmetric keys</remarks>
static class SymmetricGCM
{
	/// <summary>Encrypt span of bytes using the password</summary>
	public static byte[] encrypt( ReadOnlySpan<byte> data, ReadOnlySpan<byte> password )
	{
		validatePassword( password );

		byte[] result = new byte[ 16 + cbNonce + cbTag + data.Length ];
		Span<byte> span = result;

		headerBytes.CopyTo( span );
		span = span.Slice( 16 );

		Span<byte> nonce = span.Slice( 0, cbNonce );
		RandomNumberGenerator.Fill( nonce );
		span = span.Slice( cbNonce );

		Span<byte> key = stackalloc byte[ cbKey ];
		try
		{
			deriveKey( password, nonce, key );
			Span<byte> tag = span.Slice( 0, cbTag );
			Span<byte> ciphertext = span.Slice( cbTag );
			using AesGcm aes = new AesGcm( key, cbTag );
			aes.Encrypt( nonce, data, ciphertext, tag );
			return result;
		}
		catch
		{
			CryptographicOperations.ZeroMemory( result );
			throw;
		}
		finally
		{
			CryptographicOperations.ZeroMemory( key );
		}
	}

	/// <summary>True when the encrypted blob is long enough, and starts with the expected header</summary>
	public static bool ciphertextValid( ReadOnlySpan<byte> data )
	{
		const int minLength = 16 + cbNonce + cbTag;
		if( data.Length < minLength )
			return false;
		return headerBytes.SequenceEqual( data.Slice( 0, 16 ) );
	}

	/// <summary>Decrypt span of bytes using the password</summary>
	public static SecretBytes decrypt( ReadOnlySpan<byte> data, ReadOnlySpan<byte> password )
	{
		validatePassword( password );
		if( !ciphertextValid( data ) )
			throw new ArgumentException( "The cyphertext is invalid" );

		ReadOnlySpan<byte> nonce = data.Slice( 16, cbNonce );
		ReadOnlySpan<byte> tag = data.Slice( 16 + cbNonce, cbTag );
		ReadOnlySpan<byte> ciphertext = data.Slice( 16 + cbNonce + cbTag );

		Span<byte> key = stackalloc byte[ cbKey ];
		try
		{
			deriveKey( password, nonce, key );

			using SecretBytes plaintext = new( ciphertext.Length );
			using( AesGcm aes = new( key, cbTag ) )
				aes.Decrypt( nonce, ciphertext, tag, plaintext.span );
			return plaintext.move();
		}
		finally
		{
			CryptographicOperations.ZeroMemory( key );
		}
	}

	static ReadOnlySpan<byte> headerBytes =>
	[
		// Made with RandomNumberGenerator.Fill at design time
		0x51, 0x68, 0x70, 0x77, 0x0E, 0x79, 0x7A, 0x4C, 0x08, 0xE0, 0xB8, 0x0E, 0xF8, 0xBE, 0xE3, 0x1C
	];

	static void validatePassword( ReadOnlySpan<byte> bytes )
	{
		if( bytes.Length != 16 )
			throw new ArgumentException( "Expected 16 bytes in the password" );
	}

	// Using AES-256; keys are 32 bytes
	const int cbKey = 32;
	const int cbNonce = 12;
	const int cbTag = 16;

	/// <summary>Derive a 32-byte AES-256 key from the 16-byte password, bound to the nonce</summary>
	static void deriveKey( ReadOnlySpan<byte> password, ReadOnlySpan<byte> nonce, Span<byte> key )
	{
		Debug.Assert( key.Length == cbKey );
		HKDF.DeriveKey( HashAlgorithmName.SHA256, password, key, nonce, headerBytes );
	}
}