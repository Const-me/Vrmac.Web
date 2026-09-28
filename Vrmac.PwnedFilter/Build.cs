using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Vrmac.Pwned;
namespace Vrmac.PwnedFilter;

static class Build
{
	/// <summary>Implements the <c>PwnedFilter build</c> command</summary>
	public static void buildFilter( string[] args )
	{
		if( args.Length < 3 )
			throw new ArgumentException( "Please specify input and output files" );
		// Allocate 4GB or memory for the Bloom filter being built. The initial content is all zeros
		// The implementation uses `uint[][]` type to workaround .NET limit of the array size.
		BloomFilterBuilder builder = new();

		// Parse the source dataset, build the filter in memory
		string path = args[ 1 ];
		if( File.Exists( path ) )
			buildSingle( builder, path );
		else if( Directory.Exists( path ) )
			buildMulti( builder, path );
		else
			throw new ArgumentException( "The input is neither a file nor a directory: " + path );

		// Save the output 4GB binary file
		path = args[ 2 ];
		builder.save( path );
		Console.WriteLine( "Built the Bloom filter; saved there: {0}", path );
	}

	static void buildSingle( BloomFilterBuilder builder, string path )
	{
		Action<RentedArray> act = ( RentedArray a ) => computeChunk( builder, a );
		Parallel.ForEach( readChunks( path ), act );
	}

	static void buildMulti( BloomFilterBuilder builder, string folder )
	{
		Action<string> act = delegate ( string name )
		{
			string path = Path.Combine( folder, name );
			computeFile( builder, path, name );
		};
		Parallel.ForEach( MultiFiles.listFiles( folder ), act );
	}

	/// <summary>Read 4MB chunks from the text file splitting on new lines,
	/// produce sequence of byte arrays rented from the pool</summary>
	static IEnumerable<RentedArray> readChunks( string pathSource )
	{
		// Parse that 84.4GB ASCII text file into 4MB blocks, splitting at line ends
		foreach( ReadOnlyMemory<byte> mem in TextReader.parseChunks( pathSource ) )
			yield return new RentedArray( mem.Span );
	}

	/// <summary>Parse all lines in the chunk, set bits in the bloom filter being built</summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	static void computeChunk( BloomFilterBuilder builder, RentedArray rented )
	{
		using RentedArray raii = rented;
		Span<byte> sha1 = stackalloc byte[ 20 ];
		Span<ulong> hashes = stackalloc ulong[ BloomFilterHashes.countHashes ];

		foreach( ReadOnlyMemory<byte> line in TextReader.parseLines( rented.memory ) )
			computeEntry( builder, line.Span, sha1, hashes );
	}

	/// <summary>Load text file with a piece of the dataset into RAM, set bits in the bloom filter being built</summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	static void computeFile( BloomFilterBuilder builder, string path, ReadOnlySpan<char> name )
	{
		// Stack allocate the ASCII buffer for the complete SHA1
		Span<byte> shaAscii = stackalloc byte[ 40 ];
		// Copy ASCII file name of the piece into the initial slice of the ASCII buffer
		int ccPrefix = Encoding.ASCII.GetBytes( Path.GetFileNameWithoutExtension( name ), shaAscii );

		Span<byte> sha1 = stackalloc byte[ 20 ];
		Span<ulong> hashes = stackalloc ulong[ BloomFilterHashes.countHashes ];
		foreach( ReadOnlyMemory<byte> line in MultiFiles.parseLines( path ) )
			computeEntry( builder, line.Span, sha1, hashes, shaAscii, ccPrefix );
	}

	/// <summary>Parse a single line from <c>pwned.txt</c>, set bits in the bloom filter being built</summary>
	static void computeEntry( BloomFilterBuilder builder, ReadOnlySpan<byte> ascii,
		Span<byte> sha1, Span<ulong> hashes )
	{
		// Strip the breach count integer at the end
		int idx = ascii.IndexOf( (byte)':' );
		if( idx <= 0 )
			throw new ArgumentException();
		ascii = ascii.Slice( 0, idx );

		// Parse hexadecimal SHA1 into bytes
		OperationStatus s = Convert.FromHexString( ascii, sha1, out _, out int cb );
		if( s != OperationStatus.Done || cb != 20 )
			throw new ArgumentException();

		// Compute these 12 hashes
		BloomFilterHashes.compute( hashes, sha1 );

		// Update the bitmap with atomic OR instructions
		for( int i = 0; i < hashes.Length; i++ )
			builder.setBit( hashes[ i ] );
	}

	/// <summary>Parse a single line of a partial file, set bits in the bloom filter being built</summary>
	static void computeEntry( BloomFilterBuilder builder, ReadOnlySpan<byte> ascii,
		Span<byte> sha1, Span<ulong> hashes, Span<byte> shaAscii, int prefixLength )
	{
		Debug.Assert( sha1.Length == 20 && shaAscii.Length == 40 );

		// Strip the breach count integer at the end
		int idx = ascii.IndexOf( (byte)':' );
		if( idx <= 0 )
			throw new ArgumentException();
		ascii = ascii.Slice( 0, idx );

		// Copy into the tail half of the ASCII buffer
		if( ascii.Length + prefixLength != shaAscii.Length )
			throw new ArgumentException();
		ascii.CopyTo( shaAscii.Slice( prefixLength ) );

		// Parse hexadecimal SHA1 into bytes
		OperationStatus s = Convert.FromHexString( shaAscii, sha1, out _, out int cb );
		if( s != OperationStatus.Done || cb != 20 )
			throw new ArgumentException();

		// Compute these 12 hashes
		BloomFilterHashes.compute( hashes, sha1 );

		// Update the bitmap with atomic OR instructions
		for( int i = 0; i < hashes.Length; i++ )
			builder.setBit( hashes[ i ] );
	}
}