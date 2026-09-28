using System.Security.Cryptography;
namespace Vrmac.Security;

/// <summary>ECDH key pair for one-way backup encryption</summary>
public readonly struct BackupKeyPair
{
	/// <summary>Public key of the pair, plain text</summary>
	public readonly ReadOnlyMemory<byte> publicKey;

	/// <summary>Private key of the pair, encrypted with the password passed to <see cref="generate" /> method</summary>
	public readonly ReadOnlyMemory<byte> privateKey;

	/// <summary>Generate a new key pair, and encrypt the private key</summary>
	/// <remarks>Use <see cref="SymmetricCrypto.generatePassword" /> to generate the symmetric password</remarks>
	public static BackupKeyPair generate( string password )
	{
		using ECDiffieHellman ecdh = ECDiffieHellman.Create( curve );
		byte[] privPlain = ecdh.ExportECPrivateKey();
		byte[] priv;
		try
		{
			priv = SymmetricCrypto.encrypt( privPlain, password );
		}
		finally
		{
			CryptographicOperations.ZeroMemory( privPlain );
		}

		byte[] pub = ecdh.ExportSubjectPublicKeyInfo();
		return new BackupKeyPair( pub, priv );
	}

	internal static ECCurve curve => ECCurve.NamedCurves.nistP521;

	BackupKeyPair( ReadOnlyMemory<byte> publicKey, ReadOnlyMemory<byte> privateKey )
	{
		this.publicKey = publicKey;
		this.privateKey = privateKey;
	}
}