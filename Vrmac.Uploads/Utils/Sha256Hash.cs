using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Vrmac.Uploads;

/// <summary>Utility structure to keep SHA-256 hash sum</summary>
readonly struct Sha256Hash
{
	readonly ulong a, b, c, d;

	/// <summary>Create from the first 32 bytes of the buffer</summary>
	public static Sha256Hash create( ReadOnlySpan<byte> span )
	{
		if( span.Length < 32 )
			throw new ArgumentException();
		return new Sha256Hash( MemoryMarshal.Cast<byte, ulong>( span.Slice( 0, 32 ) ) );
	}

	/// <summary>Create the incremental hash object</summary>
	public static IncrementalHash createHash() => IncrementalHash.CreateHash( HashAlgorithmName.SHA256 );

	/// <summary>True when the hashes match</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public bool verify( IncrementalHash hash )
	{
		Debug.Assert( hash.HashLengthInBytes == 32 );
		Span<ulong> span = stackalloc ulong[ 4 ];
		hash.GetCurrentHash( MemoryMarshal.AsBytes( span ) );

		ulong x = span[ 0 ] ^ a;
		x |= span[ 1 ] ^ b;
		x |= span[ 2 ] ^ c;
		x |= span[ 3 ] ^ d;
		return x == 0;
	}

	Sha256Hash( ReadOnlySpan<ulong> span )
	{
		a = span[ 0 ];
		b = span[ 1 ];
		c = span[ 2 ];
		d = span[ 3 ];
	}
}