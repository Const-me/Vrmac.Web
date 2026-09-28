namespace Vrmac.MaxMind.Reshape;

/// <summary>Tree serializer which produces 16-wide nodes, each iteration of the lookup loop consumes 4 address bits</summary>
sealed class TreeTemp16
{
	/// <summary>Hash map to find location of nodes in the flat collection</summary>
	/// <remarks>The collection only includes nodes with at least 1 child node</remarks>
	readonly Dictionary<TreeNode, int> indices = new();
	/// <summary>Nodes in the serialised order including leaves; 2 values per node.</summary>
	readonly List<object?> serialised = new();
	// Unlike TreeTempBinary version, can't use indices hash map for loop detection because only few of the nodes are there
	readonly HashSet<TreeNode> loopDetector = new();

	/// <summary>Leaf-only tree nodes</summary>
	readonly List<ulong> leavesData = new();
	/// <summary>No reason to store duplicate leaves, the following hash maps unduplicvates them</summary>
	readonly Dictionary<ulong, int> leavesUndupe = new();
	/// <summary>Hash map to find indices of leaf nodes</summary>
	/// <remarks>The collection only includes child-free nodes</remarks>
	readonly Dictionary<TreeNode, int> leafIndices = new();

	// Each level of the output tree has exactly 16 children, indexed with 4 bit slices of the IP addresses
	const int width = 16;

	int countNodes()
	{
		int length = serialised.Count;
		if( 0 != ( length % width ) )
			throw new ApplicationException();
		return length / width;
	}

	/// <summary>Recursively serialise the tree into BFS order</summary>
	public int addTree( TreeNode root )
	{
		int rootIndex = countNodes();

		List<TreeNode> currentLevel = [ root ];
		while( currentLevel.Count > 0 )
		{
			int begin = serialised.Count;
			foreach( TreeNode node in currentLevel )
			{
				if( !loopDetector.Add( node ) )
					throw new ArgumentException( "Detected a loop" );

				int beginNode = serialised.Count;
				addIntermediate( node.left, width / 2 );
				addIntermediate( node.right, width / 2 );

				if( compressLeafNode( beginNode ) is ulong leaf )
				{
					serialised.RemoveRange( beginNode, width );
					ref int idx = ref CollectionsMarshal.GetValueRefOrAddDefault( leavesUndupe, leaf, out bool wasThere );
					if( !wasThere )
					{
						idx = leavesData.Count;
						leavesData.Add( leaf );
					}
					leafIndices.Add( node, idx );
				}
				else
					indices.Add( node, indices.Count );
			}

			currentLevel.Clear();
			foreach( object? obj in serialised.Skip( begin ) )
			{
				if( null == obj )
					continue;
				if( obj is eRegion leaf )
					continue;
				currentLevel.Add( (TreeNode)obj );
			}
		}
		return rootIndex;
	}

	void addIntermediate( object? obj, int count )
	{
		if( null == obj || obj is eRegion )
		{
			for( int i = 0; i < count; i++ )
				serialised.Add( obj );
			return;
		}

		TreeNode node = (TreeNode)obj;
		if( !loopDetector.Add( node ) )
			throw new ArgumentException( "Detected a loop" );
		if( count == 2 )
		{
			serialised.Add( node.left );
			serialised.Add( node.right );
			return;
		}
		if( count > 2 )
		{
			count /= 2;
			addIntermediate( node.left, count );
			addIntermediate( node.right, count );
			return;
		}
		throw new ApplicationException();
	}

	/// <summary>If none of the 16 most recently added objects are tree nodes, pack the payload into 4 bits/element and return the bitmap.<br/>
	/// If at least 1 object is a tree node, return null.</summary>
	ulong? compressLeafNode( int begin )
	{
		Debug.Assert( serialised.Count == begin + width );
		ulong result = 0;
		for( int i = 0; i < width; i++ )
		{
			object? obj = serialised[ begin + i ];
			if( null == obj )
				continue;   // Unassigned is encoded as zero
			if( obj is eRegion reg )
			{
				ulong val = (byte)reg;
				Debug.Assert( val < 0x10 );

				val <<= ( i * 4 );
				result |= val;
				continue;
			}
			if( obj is TreeNode )
				return null;
			throw new ApplicationException();
		}
		return result;
	}

	public void write( Stream gzip, int root4, int root6 )
	{
		int nonLeafNodes = indices.Count;
		int leafNodes = leavesData.Count;
		int totalNodes = nonLeafNodes + leafNodes;

		// Assemble the payload blob
		int cbPayload = nonLeafNodes * width * 3;
		cbPayload += leafNodes * 8;
		byte[] arr = new byte[ cbPayload ];
		{
			Span<byte> span = arr;
			foreach( object? obj in serialised )
			{
				int val;
				switch( obj )
				{
					case null:
						val = totalNodes;
						break;
					case eRegion leaf:
						val = totalNodes + 1 + (byte)leaf;
						break;
					case TreeNode node:
						if( indices.TryGetValue( node, out val ) )
							break;
						val = leafIndices[ node ] + nonLeafNodes;
						break;
					default:
						throw new ApplicationException( "Unexpected object type in the tree" );
				}
				BinaryPrimitives.WriteInt32LittleEndian( span, val );
				span = span.Slice( 3 );
			}

			ReadOnlySpan<byte> leavesData = MemoryMarshal.AsBytes( CollectionsMarshal.AsSpan( this.leavesData ) );
			if( leavesData.Length != span.Length )
				throw new ApplicationException();
			leavesData.CopyTo( span );
		}

		// Write into the GZip compressor, after the package header
		ReadOnlySpan<GeoPackage.Header> header = [ new GeoPackage.Header( arr.Length, totalNodes, nonLeafNodes, root4, root6 ) ];
		gzip.Write( MemoryMarshal.AsBytes( header ) );
		gzip.Write( arr );
	}
}