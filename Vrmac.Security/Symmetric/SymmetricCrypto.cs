using System.Buffers.Text;
using System.Security.Cryptography;
using Vrmac.Security.Symmetric;
namespace Vrmac.Security;

/// <summary>Utility functions to encrypt and decrypt secrets with a symmetric key</summary>
/// <remarks>The cryptography is only good for moderate volume of messages encrypted with the same password, below 1 billion.<br/>
/// Beyond that, GCM nonce collision breaks things catastrophically;<br/>
/// not just for the colliding message, all of them encrypted with the same password.<br/>
/// Use something else for high volumes.</remarks>
public static class SymmetricCrypto
{
	/// <summary>Generate a strong password</summary>
	/// <remarks>If you want the password i.e. it's not immediately deployed to a server, please save in a password manager</remarks>
	public static string generatePassword()
	{
		Span<byte> span = stackalloc byte[ 16 ];
		RandomNumberGenerator.Fill( span );
		try
		{
			return Base64Url.EncodeToString( span );
		}
		finally
		{
			CryptographicOperations.ZeroMemory( span );
		}
	}

	/// <summary>Encrypt array of bytes using the string password</summary>
	public static byte[] encrypt( byte[] data, string password )
	{
		using SecretBytes passwordBytes = new( Base64Url.DecodeFromChars( password ) );
		if( passwordBytes.length != 16 )
			throw new ArgumentException( "Expected a password made with generatePassword() function" );
		return encrypt( data, passwordBytes );
	}

	/// <summary>Encrypt array of bytes using the password</summary>
	public static byte[] encrypt( ReadOnlySpan<byte> data, ReadOnlySpan<byte> passwordBytes )
	{
		// When encrypting the data, only use the newer GCM implementation
		return SymmetricGCM.encrypt( data, passwordBytes );
	}

	/// <summary>Decrypt array of bytes using the string password</summary>
	public static SecretBytes decrypt( byte[] data, string password )
	{
		using SecretBytes passwordBytes = new( Base64Url.DecodeFromChars( password ) );
		if( passwordBytes.length != 16 )
			throw new ArgumentException( "Expected a password made with generatePassword() function" );
		return decrypt( data, passwordBytes );
	}

	/// <summary>Decrypt array of bytes using the binary password</summary>
	public static SecretBytes decrypt( byte[] data, ReadOnlySpan<byte> password )
	{
		ReadOnlySpan<byte> span = data;
		if( SymmetricGCM.ciphertextValid( span ) )
			return SymmetricGCM.decrypt( span, password );
#if SUPPORT_LEGACY_CRYPTO
		// The following line is compiled away from the version published on nuget
		return SymmetricCBC.decrypt( data, password );
#else
		throw new ArgumentException( "The cyphertext is invalid" );
#endif
	}
}