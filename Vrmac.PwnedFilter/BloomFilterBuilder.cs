using System.Runtime.InteropServices;
using Vrmac.Pwned;
namespace Vrmac.PwnedFilter;

/// <summary>Utility class to build the bloom filter in memory</summary>
sealed class BloomFilterBuilder
{
	// The magic number is 32 gigabit
	const ulong filterBits = BloomFilterHashes.filterBits;
	const ulong bitsPerBlock = 8UL << 30;
	readonly uint[][] blocks;

	public BloomFilterBuilder()
	{
		if( 0 != ( filterBits % bitsPerBlock ) )
			throw new ApplicationException();

		const int countBlocks = (int)( filterBits / bitsPerBlock );
		blocks = new uint[ countBlocks ][];

		const int eltsPerBlock = (int)( bitsPerBlock / 32 );
		for( int i = 0; i < countBlocks; i++ )
			blocks[ i ] = new uint[ eltsPerBlock ];
	}

	public void setBit( ulong idx )
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual( idx, filterBits );

		int idxBlock = (int)( idx / bitsPerBlock );
		uint[] block = blocks[ idxBlock ];
		idx %= bitsPerBlock;

		int eltIndex = (int)( idx / 32 );
		int bitindex = (int)( idx % 32 );
		uint bit = 1u << bitindex;
		Interlocked.Or( ref block[ eltIndex ], bit );
	}

	public void save( string path )
	{
		using FileStream file = File.Create( path );
		foreach( uint[] block in blocks )
		{
			ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes( block );
			file.Write( bytes );
		}
	}
}