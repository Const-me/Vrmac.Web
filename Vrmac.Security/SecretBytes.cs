using System.Security.Cryptography;
namespace Vrmac.Security;

/// <summary>Array of bytes with security sensitive data in managed memory</summary>
/// <remarks>Making copies with language built-in assignment or copy breaks the ownership model; don’t do that.<br/>
/// The <c>Dispose</c> method clears the array.</remarks>
public ref struct SecretBytes: IDisposable
{
	byte[]? bytes;

	/// <summary>Assign ownership of the array</summary>
	public SecretBytes( byte[] bytes ) => this.bytes = bytes;
	/// <summary>Copy span into a newly allocated array</summary>
	public SecretBytes( ReadOnlySpan<byte> span ) => bytes = span.ToArray();
	/// <summary>Allocate a new array; initial content is all zeros.</summary>
	public SecretBytes( int length ) => bytes = new byte[ length ];

	/// <summary>Length of the array in bytes</summary>
	public int length
	{
		get
		{
			throwIfDisposed();
			return bytes!.Length;
		}
	}

	/// <summary>Destroy the data</summary>
	public void Dispose()
	{
		byte[]? arr = Interlocked.Exchange( ref bytes, null );
		CryptographicOperations.ZeroMemory( arr );
	}

	void throwIfDisposed() =>
		ObjectDisposedException.ThrowIf( null == bytes, typeof( SecretBytes ) );

	/// <summary>Implicit cast to ReadOnlySpan</summary>
	/// <remarks>Throws an exception when the structure is not initialised, or has been disposed.</remarks>
	public static implicit operator ReadOnlySpan<byte>( SecretBytes sb )
	{
		sb.throwIfDisposed();
		return sb.bytes;
	}

	/// <summary>Convert to span</summary>
	/// <remarks>Throws an exception when the structure is not initialised, or has been disposed.</remarks>
	public Span<byte> span
	{
		get
		{
			throwIfDisposed();
			return bytes;
		}
	}

	/// <summary>Convert to ReadOnlyMemory</summary>
	/// <remarks>Throws an exception when the structure is not initialised, or has been disposed.</remarks>
	public ReadOnlyMemory<byte> memory
	{
		get
		{
			throwIfDisposed();
			return bytes;
		}
	}

	/// <summary>Get the stored array of bytes</summary>
	/// <remarks><c>Aes</c> class from the standard library can't operate on spans, it requires arrays.<br/>
	/// Throws an exception when the structure is not initialised, or has been disposed.</remarks>
	public byte[] array
	{
		get
		{
			throwIfDisposed();
			return bytes!;
		}
	}

	/// <summary>Move ownership into a new copy of the same structure</summary>
	/// <remarks>Throws an exception when the structure is not initialised, or has been disposed.</remarks>
	public SecretBytes move() => new SecretBytes( detach() );

	/// <summary>this.Dispose(); this = rhs;</summary>
	public void assign( byte[] rhs )
	{
		byte[]? prev = Interlocked.Exchange( ref bytes, rhs );
		if( !ReferenceEquals( rhs, prev ) )
			CryptographicOperations.ZeroMemory( prev );
	}

	/// <summary>Move ownership of the array out of this instance</summary>
	/// <remarks>Throws an exception when the structure is not initialised, or has been disposed.</remarks>
	public byte[] detach()
	{
		byte[]? arr = Interlocked.Exchange( ref bytes, null );
		ObjectDisposedException.ThrowIf( null == arr, typeof( SecretBytes ) );
		return arr;
	}
}