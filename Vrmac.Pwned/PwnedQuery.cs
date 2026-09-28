using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
namespace Vrmac.Pwned;

/// <summary>Utility class to query "Have I Been Pwned?" Bloom filter</summary>
public sealed class PwnedQuery: IDisposable
{
	/// <summary>Open Bloom filter file on disk</summary>
	/// <remarks>The constructor doesn't immediately read any bytes from there,<br/>
	/// but note that in extreme cases this class may consume up to 4GB of RAM.</remarks>
	public PwnedQuery( string path )
	{
		FileInfo fileInfo = new FileInfo( path );
		if( !fileInfo.Exists )
			throw new FileNotFoundException();

		const long cbSize = (long)( BloomFilterHashes.filterBits / 8 );
		if( fileInfo.Length != cbSize )
			throw new ArgumentException();

		file = MemoryMappedFile.CreateFromFile( path, FileMode.Open, null, cbSize, MemoryMappedFileAccess.Read );
		try
		{
			view = file.CreateViewAccessor( 0, cbSize, MemoryMappedFileAccess.Read );
		}
		catch
		{
			file.Dispose();
			throw;
		}

		try
		{
			unsafe
			{
				byte* ptr = null;
				view.SafeMemoryMappedViewHandle.AcquirePointer( ref ptr );
				mappedPointer = (nint)ptr;
			}
		}
		catch
		{
			view.Dispose();
			file.Dispose();
			throw;
		}
	}

	nint mappedPointer;
	readonly MemoryMappedFile file;
	readonly MemoryMappedViewAccessor view;

	/// <summary>Maximum length of the input password, expressed in UTF-16 code units</summary>
	const int maxPasswordLength = 512;

	/// <summary>Returns true when the input password is on the list</summary>
	/// <remarks>The method is thread-safe and reentrant.<br/>
	/// Note there's a small probability of false positives.
	/// In theory i.e. in the absence of software bugs, that probability should be 0.034%.</remarks>
	[MethodImpl( MethodImplOptions.NoInlining )]
	public bool query( string passwordText )
	{
		// Restricting length to avoid stack overflow exceptions for malicious input
		ArgumentOutOfRangeException.ThrowIfGreaterThan( passwordText.Length, maxPasswordLength );

		Encoding encoding = Encoding.UTF8;
		int cb = encoding.GetByteCount( passwordText );
		Span<byte> password = stackalloc byte[ cb ];
		encoding.GetBytes( passwordText, password );
		Span<byte> sha1 = stackalloc byte[ 20 ];
		SHA1.HashData( password, sha1 );

		Span<ulong> hashes = stackalloc ulong[ BloomFilterHashes.countHashes ];
		BloomFilterHashes.compute( hashes, sha1 );
		hashes.Sort();

		for( int i = 0; i < BloomFilterHashes.countHashes; i++ )
		{
			if( hashBitMissing( hashes[ i ] ) )
				return false;
		}
		return true;
	}

	/// <summary>True when the <c>pwned.bin</c> file <b>does not</b> have a set bit with the provided index</summary>
	bool hashBitMissing( ulong idx )
	{
		long byteOffset = (long)( idx / 8 );
		byte elt;
		unsafe
		{
			byte* rsi = (byte*)mappedPointer;
			elt = rsi[ byteOffset ];
		}
		byte bit = (byte)( 1u << (int)( idx % 8 ) );
		return 0 == ( elt & bit );
	}

	/// <summary>Unmap the filter and release the resources</summary>
	public void Dispose()
	{
		mappedPointer = 0;
		view.SafeMemoryMappedViewHandle.ReleasePointer();
		view.Dispose();
		file.Dispose();
	}
}