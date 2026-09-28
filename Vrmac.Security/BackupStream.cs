using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Vrmac.Security;

/// <summary>Utility class implementing write-only stream for one-way encryption of backups.</summary>
/// <remarks>It uses ECDH on the P521 curve to derive symmetric key, then AES-256 CBC to encrypt.<br/>
/// Because AES CBC doesn’t offer integrity protection, this class also computes SHA-512 of the plain text, stores in the header of the encrypted file.</remarks>
public sealed class BackupStream: Stream
{
	/// <summary>Create encrypting stream for a new backup</summary>
	/// <param name="container">Destination stream</param>
	/// <param name="publicKey">Public half of the NIST P521 backup key, plain text</param>
	/// <param name="formatVersion">Signature marker of the output file</param>
	public BackupStream( FileStream container, byte[] publicKey, in Guid formatVersion )
	{
		container.Seek( 0, SeekOrigin.Begin );
		container.SetLength( 0 );

		using ECDiffieHellman session = ECDiffieHellman.Create( BackupKeyPair.curve );
		byte[] backupPublicKey = session.PublicKey.ExportSubjectPublicKeyInfo();

		using ECDiffieHellman stable = ECDiffieHellman.Create();
		stable.ImportSubjectPublicKeyInfo( publicKey, out _ );
		using SecretBytes symmetricKey = new( session.DeriveKeyMaterial( stable.PublicKey ) );

		aes = Aes.Create();
		try
		{
			aes.KeySize = 256;
			aes.Mode = CipherMode.CBC;
			aes.Padding = PaddingMode.PKCS7;
			// Reduce symmetric key into the 32 bytes we need for the AES
			aes.Key = SHA256.HashData( symmetricKey );
			// That structure handles double dispose correctly; call ZeroMemory ASAP
			symmetricKey.Dispose();
			aes.GenerateIV();

			byte[] iv = aes.IV;
			// Include 64 bytes of zeros reserving space for the SHA-512 hash
			Span<byte> header = stackalloc byte[ 16 + 64 + 2 + 2 ];
			header.Clear();

			formatVersion.TryWriteBytes( header );
			const int offset = 16 + 64;
			BinaryPrimitives.WriteUInt16LittleEndian( header.Slice( offset ), (ushort)backupPublicKey.Length );
			BinaryPrimitives.WriteUInt16LittleEndian( header.Slice( offset + 2 ), (ushort)iv.Length );
			container.Write( header );
			container.Write( backupPublicKey );
			container.Write( iv );

			cryptoStream = new CryptoStream( container, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true );

			sha = IncrementalHash.CreateHash( HashAlgorithmName.SHA512 );
		}
		catch
		{
			aes.Dispose();
			throw;
		}

		Debug.Assert( sha.HashLengthInBytes == 64 );
		this.container = container;
	}

	/// <summary>Incremental SHA-512 hash object</summary>
	readonly IncrementalHash sha;
	/// <summary>AES-256 CBC encryptor</summary>
	readonly CryptoStream cryptoStream;
	/// <summary>Current write offset</summary>
	long written = 0;
	/// <summary>AES object</summary>
	readonly Aes aes;
	/// <summary>Destination file being written</summary>
	readonly FileStream container;

	void write( ReadOnlySpan<byte> span )
	{
		sha.AppendData( span );
		cryptoStream.Write( span );
		written += span.Length;
	}
	/// <inheritdoc />
	public override void Write( ReadOnlySpan<byte> buffer ) => write( buffer );
	/// <inheritdoc />
	public override void Write( byte[] buffer, int offset, int count ) => write( buffer.AsSpan( offset, count ) );

	/// <inheritdoc />
	protected override void Dispose( bool disposing )
	{
		// Finalize the encrypting stream
		cryptoStream.FlushFinalBlock();
		cryptoStream.Dispose();
		aes.Dispose();

		// Finalize SHA-512 of the plaintext
		byte[] hash = sha.GetHashAndReset();
		sha.Dispose();

		// Write the SHA-512 hash to the start of the file, 16 bytes after the begin
		container.Seek( 16, SeekOrigin.Begin );
		container.Write( hash );

		// Flush and close the output file
		container.Flush( flushToDisk: true );
		container.Dispose();
		base.Dispose( disposing );
	}
	/// <inheritdoc />
	public override void Flush() { }
	/// <inheritdoc />
	public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
	/// <inheritdoc />
	public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
	/// <inheritdoc />
	public override void SetLength( long value ) => throw new NotSupportedException();

	/// <inheritdoc />
	public override bool CanRead => false;
	/// <inheritdoc />
	public override bool CanSeek => false;
	/// <inheritdoc />
	public override bool CanWrite => true;
	/// <inheritdoc />
	public override long Length => written;
	/// <inheritdoc />
	public override long Position
	{
		get => written;
		set => throw new NotSupportedException();
	}
}