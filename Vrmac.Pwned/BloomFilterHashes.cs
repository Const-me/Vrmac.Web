using System.Runtime.InteropServices;
namespace Vrmac.Pwned;

/// <summary>Setup of the Bloom filter for "Have I Been Pwned?" password hash database</summary>
/// <remarks>The OG dataset has 2068408781 SHA1 hashes.<br/>
/// Some calculator web page says with 4GB in the filter and 12 hash functions, the probability of false positives is 0.034% i.e. 1 in 2913</remarks>
/// <seealso cref="PwnedQuery" />
static class BloomFilterHashes
{
	// https://hur.st/bloomfilter/?n=2068408781&p=&m=4GiB&k=12

	/// <summary>12 hash functions</summary>
	public const int countHashes = 12;
	/// <summary>4 GB = 32 gigabits in the Bloom filter</summary>
	public const ulong filterBits = ( 8UL * 4 ) << 30;

	/// <summary>Compute indices of the 12 bits for the Bloom filter</summary>
	public static void compute( Span<ulong> hashes, ReadOnlySpan<byte> sha1 )
	{
		ArgumentOutOfRangeException.ThrowIfNotEqual( sha1.Length, 20 );
		ArgumentOutOfRangeException.ThrowIfNotEqual( hashes.Length, countHashes );
		// 12 hash functions each generating a number in [ 0 .. filterBits - 1 ] range

		const ulong mask = filterBits - 1UL;
		ulong h1 = MemoryMarshal.Read<ulong>( sha1 );
		ulong h2 = MemoryMarshal.Read<ulong>( sha1.Slice( 8 ) );
		uint tail = MemoryMarshal.Read<uint>( sha1.Slice( 16 ) );
		unchecked
		{
			h2 ^= (ulong)tail << 32;
			h2 |= 1;
			for( int i = 0; i < countHashes; i++ )
				hashes[ i ] = ( h1 + (ulong)i * h2 ) & mask;
		}
	}
}