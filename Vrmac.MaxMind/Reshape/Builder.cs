using System.IO.Compression;
namespace Vrmac.MaxMind.Reshape;

/// <summary>Temporary data used during converting of the GeoIP DB from mmdb to my optimised format</summary>
sealed class Builder
{
	public readonly byte recordSize;
	public readonly uint countNodes;
	readonly byte[] treeBytes;
	public Builder( int recordSize, long countNodes, ReadOnlySpan<byte> treeBytes )
	{
		if( recordSize != 24 )
			throw new ArgumentException( "GeoIP exporter only supports 24-bit nodes" );

		this.recordSize = (byte)recordSize;
		this.countNodes = (uint)countNodes;
		int length = treeBytes.Length;
		// Allocating 1 extra tree bytes to be able to load without branches even the very last integer using 4-byte load instruction
		this.treeBytes = new byte[ length + 1 ];

		int totalIntegers = (int)countNodes * 2;
		if( totalIntegers * 3 != length )
			throw new ApplicationException();

		fixRetardedByteOrder( this.treeBytes, treeBytes, totalIntegers );
	}

	void fixRetardedByteOrder( Span<byte> destination, ReadOnlySpan<byte> source, int length )
	{
		int end = length - 1;
		for( int i = 0; i < end; i++ )
		{
			ReadOnlySpan<byte> src = source.Slice( i * 3, 4 );
			Span<byte> dst = destination.Slice( i * 3, 4 );
			uint u = BinaryPrimitives.ReadUInt32BigEndian( src );
			u >>= 8;
			BinaryPrimitives.WriteUInt32LittleEndian( dst, u );
		}

		{
			ReadOnlySpan<byte> src = source.Slice( end * 3, 3 );
			int i = ( src[ 0 ] << 16 ) | ( src[ 1 ] << 8 ) | src[ 2 ];
			Span<byte> dst = destination.Slice( end * 3, 4 );
			BinaryPrimitives.WriteInt32LittleEndian( dst, i );
		}
	}

	readonly Dictionary<long, Record> recordsTemp = new();

	/// <summary>Add the payload parsed from the OG database in MMDB format</summary>
	/// <remarks>The method is called by <see cref="Db.Reader.exportDatabaseV3" /> to populate the leaves</remarks>
	public void add( long pointer, Record record )
	{
		ref Record? rdi = ref CollectionsMarshal.GetValueRefOrAddDefault( recordsTemp, pointer, out bool wasThere );
		if( !wasThere )
		{
			rdi = record;
			return;
		}
		if( record != rdi )
			throw new ArgumentException();
	}

	/// <summary>Generate the compressed package</summary>
	public byte[] serialise()
	{
		// De-serialise flat binary tree into the object graph;
		// the followimng line allocates millions of small objects on the GC heap.
		TreeData tree = new( treeBytes, (int)countNodes, recordsTemp );

		// Verify a few assumptions about the mapped IPv4 block
		byte[] tmp = new byte[ 12 ];
		TreeNode node4 = tree.findNode( tmp );
		tmp.AsSpan( 10 ).Fill( 0xFF );
		TreeNode nodeMapped = tree.findNode( tmp );
		// Recursive subtree comparison, including leaves
		if( !node4.equalLeaves( nodeMapped ) )
			throw new ApplicationException( "MaxMind has failed database serialisation: IPv4 block does not match mapped 4 to 6" );
		if( !ReferenceEquals( node4, nodeMapped ) )
			throw new ApplicationException( "The importer has failed database serialisation: IPv4 block and mapped 4to6 supposed to be same nodes" );

		nodeMapped = tree.findNode( [ 0x20, 0x02 ] );
		if( !node4.equalLeaves( nodeMapped ) )
			throw new ApplicationException( "MaxMind has failed database serialisation: IPv4 block does not match 6to4 tunnelled" );
		if( !ReferenceEquals( node4, nodeMapped ) )
			throw new ApplicationException( "The importer has failed database serialisation: IPv4 block and mapped 6to4 tunnelled supposed to be same nodes" );

		// Resolve countries into the enum
		tree.transformLeaves( resolveRegions );
		// The following line collapses more than 300k redundant nodes where both children resolved into equal region
		int status = tree.collapseEqual();

		// Uproot the IPv4 block from the tree
		tmp = new byte[ 12 ];
		node4 = tree.extractNode( tmp );
		// Remove 3 other copies of the block mapped into random locations of the IPv6 space
		status = tree.removeAllNodes( node4 );
		Debug.Assert( status == 3 );
		// Collapse unneeded IPv6 levels after the extraction; it now has over 100 redundant nodes with both children null
		status = tree.collapseEqual();

		// Reshape both binary trees into 16-wide ones
		// On the same pass detect leaf-only nodes, remove them from the main tree, populating list of unique 64-bit bitmaps,
		// and inserting into hash map with links from nodes to leaf-only values.
		// Then serialise into BFS order. The OG database was in DFS order.
		// BFS is better because CPU cache hierarchy: the few root levels will stay L1D resident under load.
		TreeTemp16 treeTemp = new();
		int root4 = treeTemp.addTree( node4 );
		Debug.Assert( root4 == 0 );
		int root6 = treeTemp.addTree( tree.root );

		// Produce the GZip package we're after
		using MemoryStream ms = new();
		using( GZipStream gzip = new( ms, CompressionLevel.SmallestSize ) )
			treeTemp.write( gzip, root4, root6 );
		return ms.ToArray();
	}

	static object resolveRegions( object obj )
	{
		Record rec = (Record)obj;
		return rec.regionId();
	}
}