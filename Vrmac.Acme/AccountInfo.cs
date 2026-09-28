using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
namespace AcmeV2;

/// <summary>Identity information of a previously registered account</summary>
/// <remarks>The class is immutable and thread safe</remarks>
public sealed class AccountInfo
{
	/// <summary>Private ECC key</summary>
	byte[] key { get; }

	/// <summary>Client ID</summary>
	internal string id { get; }

	/// <summary>Generate a new key pair</summary>
	internal static byte[] generateNewKey()
	{
		using ECDsa dsa = ECDsa.Create( ECCurve.NamedCurves.nistP384 );
		return dsa.ExportECPrivateKey();
	}

	/// <summary>Import the private key bytes</summary>
	internal static ECDsa dsa( ReadOnlySpan<byte> key )
	{
		ECDsa dsa = ECDsa.Create();
		try
		{
			dsa.ImportECPrivateKey( key, out _ );
			return dsa;
		}
		catch
		{
			dsa.Dispose();
			throw;
		}
	}

	/// <summary>Import the private key bytes</summary>
	internal ECDsa dsa() => dsa( key );

	/// <summary>Serialise into array of bytes</summary>
	public byte[] serialise()
	{
		ReadOnlySpan<byte> key = this.key;
		ReadOnlySpan<byte> id = Encoding.UTF8.GetBytes( this.id );

		byte[] arr = new byte[ 4 + key.Length + id.Length ];
		Span<byte> span = arr;
		BinaryPrimitives.WriteUInt16LittleEndian( span, (ushort)key.Length );
		BinaryPrimitives.WriteUInt16LittleEndian( span.Slice( 2 ), (ushort)id.Length );
		key.CopyTo( span.Slice( 4 ) );
		id.CopyTo( span.Slice( 4 + key.Length ) );
		return arr;
	}

	/// <summary>Deserialise from the span of bytes</summary>
	public static AccountInfo deserialise( ReadOnlySpan<byte> span )
	{
		int keyLength = BinaryPrimitives.ReadUInt16LittleEndian( span );
		int idLength = BinaryPrimitives.ReadUInt16LittleEndian( span.Slice( 2 ) );
		byte[] key = span.Slice( 4, keyLength ).ToArray();
		string id = Encoding.UTF8.GetString( span.Slice( 4 + keyLength ) );
		return new AccountInfo( key, id );
	}

	internal AccountInfo( byte[] key, string id )
	{
		this.key = key;
		this.id = id;
	}
}