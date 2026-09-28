using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
namespace Vrmac.GeoIP;

/// <summary>In-memory GeoIP database converted from Maxmind GeoLite Country</summary>
/// <remarks>
/// <para>The data structure is designed for optimal performance of the lookups. Original research.<br/>
/// The worst case for an IPv4 lookup is 9 cache misses.<br/>The best case for IPv4 when hot in caches is ~100 CPU cycles.</para>
/// <para>The class requires AMD64 CPU with the BMI1 ISA extension.</para>
/// </remarks>
public sealed class GeoPackage
{
	/// <summary>Load from GZip stream</summary>
	public static GeoPackage load( Stream stream ) => new GeoPackage( stream );

	/// <summary>Load from GZip file on disk</summary>
	public static GeoPackage load( string path )
	{
		using FileStream stream = File.OpenRead( path );
		return new GeoPackage( stream );
	}

	GeoPackage( Stream source )
	{
		if( !( Bmi1.IsSupported && Bmi1.X64.IsSupported ) )
			throw new NotImplementedException( "GeoPackage class requires BMI1. If you are AOT compiling, please specify at least IlcInstructionSet=avx2" );

		using GZipStream gzip = new( source, CompressionMode.Decompress );
		// Load the header, and store into fields of this class
		Header h = default;
		gzip.ReadExactly( MemoryMarshal.AsBytes( MemoryMarshal.CreateSpan( ref h, 1 ) ) );
		if( h.signature != packageSignature )
			throw new ArgumentException( "Unexpected GeoIP package format" );
		countNodes = h.countNodes;
		nonLeafNodes = h.nonLeafNodes;
		if( h.root4 != root4 )
			throw new ArgumentException( "The IPv4 tree should be the first in the package" );

		root6 = h.root6;
		leafDataOffset = nonLeafNodes * 3 * nodeWidth;

		// Load the payload data
		byte[] tree = new byte[ h.bytesTree ];
		gzip.ReadExactly( tree );
		this.tree = tree;
	}

	/// <summary>Lookup region from IP address</summary>
	/// <remarks>The method is thread safe, re-entrant, and quite fast, like 21 nanoseconds micro-benchmarked on Ryzen 7 8700G</remarks>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public eRegion lookup( IPAddress address )
	{
		Span<byte> span = stackalloc byte[ 16 ];
		if( !address.TryWriteBytes( span, out int cbAddress ) )
			throw new InvalidOperationException();

		uint ip4Bytes;
		if( cbAddress == 16 )
		{
			if( isMappedIp4( span ) )
			{
				// IPv4-mapped IPv6 address
				ip4Bytes = BinaryPrimitives.ReadUInt32BigEndian( span.Slice( 12 ) );
				goto Lookup4;
			}
			if( isTunelledIp4( span ) )
			{
				// https://en.wikipedia.org/wiki/6to4
				ip4Bytes = BinaryPrimitives.ReadUInt32BigEndian( span.Slice( 2 ) );
				goto Lookup4;
			}
			if( isTeredoIp4( span ) )
			{
				// https://en.wikipedia.org/wiki/Teredo_tunneling#IPv6_addressing
				// The bitwise not operator on the next line deobfuscates the client's public IPv4
				ip4Bytes = ~BinaryPrimitives.ReadUInt32BigEndian( span.Slice( 12 ) );
				goto Lookup4;
			}
			// Actual IPv6 address
			return findAddress6( span );
		}
		else if( cbAddress == 4 )
		{
			// This branch is dead code in production.
			// It's very cheap though; left here to support programs which only handle IPv4 addresses
			ip4Bytes = BinaryPrimitives.ReadUInt32BigEndian( span );
			goto Lookup4;
		}
		else
			throw new ArgumentException();
	Lookup4:
		return findAddress4( ip4Bytes );
	}

	/// <summary>Total count of nodes in the tree, including leaf-only nodes</summary>
	readonly int countNodes;
	/// <summary>Count of nodes in the IPv4 and IPv6 trees combined, the number excludes the leaf-only nodes</summary>
	readonly int nonLeafNodes;
	/// <summary>Offset to the root of the IPv6 tree, in 48 bytes nodes</summary>
	readonly int root6;
	/// <summary>Offset to the first leaf-only node, in bytes</summary>
	readonly int leafDataOffset;

	/// <summary>Payload data decompressed from the GZip package; all integers are little endian there.</summary>
	/// <remarks>The initial portion is the IPv4 tree; each node has 16 entries 3 bytes/each, so 48 bytes per node.<br/>
	/// The next portion is the IPv6 tree, same format as the IPv4 tree.<br/>
	/// The final portion is leaf-only nodes references by both IPv4 and IPv6 trees; each node is 16 entries 4 bits/each, so 8 bytes per node.</remarks>
	readonly ReadOnlyMemory<byte> tree;

	const int root4 = 0;

	/// <summary><c>true</c> for IPv6 addresses from the "IPv4-IPv6 Translation Address" block <c>::ffff:0:0/96</c></summary>
	/// <remarks>This function is on the hot path because Alpine/Kestrel stack delivers all IPv4 addresses mapped into IPv6 space</remarks>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static bool isMappedIp4( ReadOnlySpan<byte> span )
	{
		// First 8 bytes must be zero, next 4 bytes must equal to [ 0, 0, 0xFF, 0xFF ]
		// Using bit tricks to optimise both comparisons into a single branch
		uint b = BinaryPrimitives.ReadUInt32LittleEndian( span.Slice( 8 ) );
		ulong a = BinaryPrimitives.ReadUInt64LittleEndian( span );
		b ^= 0xFFFF0000u;
		a |= b;
		return a == 0;
	}

	/// <summary><c>true</c> for IPv6 addresses mapped from IPv4 using the crazy RFC 3056 BS</summary>
	/// <seealso href="https://en.wikipedia.org/wiki/6to4"/>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static bool isTunelledIp4( ReadOnlySpan<byte> span ) =>
		// Comparing first 2 bytes for equality with big endian 0x2002
		BinaryPrimitives.ReadUInt16LittleEndian( span ) == 0x0220;

	/// <summary><c>true</c> for IPv6 addresses mapped from IPv4 using the crazy (also deprecated) RFC 4380</summary>
	/// <seealso href="https://en.wikipedia.org/wiki/Teredo_tunneling"/>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static bool isTeredoIp4( ReadOnlySpan<byte> span ) =>
		// Comparing first 4 bytes for equality with big endian 0x20010000
		BinaryPrimitives.ReadUInt32LittleEndian( span ) == 0x0120;

	/// <summary>Lookup region from the big-endian IPv4 address in a register</summary>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	eRegion findAddress4( uint ip4 )
	{
		int record = root4;
		int countNodes = this.countNodes;
		int nonLeafNodes = this.nonLeafNodes;
		ReadOnlySpan<byte> span = tree.Span;
		unchecked
		{
			const ushort controlLength = ( nodeWidthLog2 << 8 );
			const ushort controlBegin = controlLength | ( 32 - nodeWidthLog2 );
			for( ushort i = controlBegin; (byte)i < 32 && record < countNodes; i -= nodeWidthLog2 )
			{
				uint index = Bmi1.BitFieldExtract( ip4, i );
				if( record >= nonLeafNodes )
					return readLeaf( span, record - nonLeafNodes, index );
				record = readNode( span, record, (int)index );
			}
		}
		return makeRegion( record, countNodes );
	}

	/// <summary>Load region from the leaf-only node</summary>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	eRegion readLeaf( ReadOnlySpan<byte> span, int node, uint leaf )
	{
		Debug.Assert( node >= 0 && node < ( countNodes - nonLeafNodes ) );
		Debug.Assert( leaf < nodeWidth );

		// Load 8 bytes from memory
		int rsi = node * 8 + leafDataOffset;
		ulong element = BinaryPrimitives.ReadUInt64LittleEndian( span.Slice( rsi, 8 ) );
		// Extract 4 bits slice from the value
		unchecked
		{
			byte start = (byte)( leaf * 4 );
			element = Bmi1.X64.BitFieldExtract( element, start, 4 );
			return (eRegion)element;
		}
	}

	/// <summary>Compute region from a leaf of the normal tree node</summary>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static eRegion makeRegion( int record, int countNodes )
	{
		if( record == countNodes )
		{
			// record is empty
			return eRegion.Unassigned;
		}
		if( record > countNodes )
		{
			// record is a data pointer
			int result = record - ( countNodes + 1 );
			Debug.Assert( result >= 0 );
			if( result < 0 )
				return eRegion.Unassigned;
			if( result > 15 )
				throw new ArgumentException( "The GeoDB search tree is corrupt, malformed payload" );
			return unchecked((eRegion)result);
		}
		throw new ArgumentException( "The GeoDB search tree is corrupt: a record value pointed back into the search tree." );
	}

	/// <summary>Lookup region from the IPv6 address in memory</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	eRegion findAddress6( ReadOnlySpan<byte> addressBytes )
	{
		Debug.Assert( addressBytes.Length == 16 );
		int record = root6;
		int countNodes = this.countNodes;
		ReadOnlySpan<byte> span = tree.Span;

		ulong bitmap = BinaryPrimitives.ReadUInt64BigEndian( addressBytes );
		const ushort controlLength = ( nodeWidthLog2 << 8 );
		const ushort controlBegin = controlLength | ( 64 - nodeWidthLog2 );

		unchecked
		{
			for( ushort i = controlBegin; (byte)i < 64 && record < countNodes; i -= nodeWidthLog2 )
			{
				uint index = (uint)Bmi1.X64.BitFieldExtract( bitmap, i );
				if( record >= nonLeafNodes )
					return readLeaf( span, record - nonLeafNodes, index );
				record = readNode( span, record, (int)index );
			}
		}

		if( record < countNodes )
		{
			bitmap = BinaryPrimitives.ReadUInt64BigEndian( addressBytes.Slice( 8 ) );
			unchecked
			{
				for( ushort i = controlBegin; (byte)i < 64 && record < countNodes; i -= nodeWidthLog2 )
				{
					uint index = (uint)Bmi1.X64.BitFieldExtract( bitmap, i );
					if( record >= nonLeafNodes )
						return readLeaf( span, record - nonLeafNodes, index );
					record = readNode( span, record, (int)index );
				}
			}
		}
		return makeRegion( record, countNodes );
	}

	const int nodeWidthLog2 = 4;
	const int nodeWidth = 1 << nodeWidthLog2;

	/// <summary>Load 24 bits integer from the tree node</summary>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static int readNode( ReadOnlySpan<byte> tree, int nodeNumber, int index )
	{
		Debug.Assert( index >= 0 && index < nodeWidth );
		unchecked
		{
			// Byte offset of the node
			int rsi = nodeNumber * ( nodeWidth * 3 );
			// Add byte offset of the child
			rsi += index * 3;
			// Loading extra padding byte for the last child in the node is fine because the packed trees are followed by leaf-only nodes
			int e = BinaryPrimitives.ReadInt32LittleEndian( tree.Slice( rsi, 4 ) );
			// Clear the padding byte (most significant one because little endian) with a mask
			return e & 0xFFFFFF;
		}
	}

	// random.org
	const uint packageSignature = 0xF07DFACC;

	/// <summary>Header of the package, first few bytes inside the gzip</summary>
	internal readonly struct Header
	{
		/// <summary>Magic number to identify the serialised package format</summary>
		public readonly uint signature;
		/// <summary>Count of payload bytes includes both IPv4 and IPv6 trees, and leaf-only nodes</summary>
		public readonly int bytesTree;
		/// <summary>Total count of tree nodes, including leaf-only ones</summary>
		public readonly int countNodes;
		/// <summary>Count of tree nodes in both trees, <b>excluding</b> leaf-only ones</summary>
		public readonly int nonLeafNodes;
		/// <summary>Node index of the root of the IPv4 tree</summary>
		/// <remarks>The value is always zero</remarks>
		public readonly int root4;
		/// <summary>Node index of the root of the IPv6 tree</summary>
		public readonly int root6;

		/// <summary>Constructor for the converter tool</summary>
		public Header( int bytesTree, int countNodes, int nonLeafNodes, int root4, int root6 )
		{
			signature = packageSignature;
			this.bytesTree = bytesTree;
			this.countNodes = countNodes;
			this.nonLeafNodes = nonLeafNodes;
			this.root4 = root4;
			this.root6 = root6;
		}
	}
}