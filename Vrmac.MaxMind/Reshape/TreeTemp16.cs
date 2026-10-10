using System.Numerics;
namespace Vrmac.MaxMind.Reshape;

/// <summary>Tree serializer which produces 16-wide nodes, each iteration of the lookup loop consumes 4 address bits</summary>
sealed class TreeTemp16
{
	/// <summary>Hash map to find location of nodes in the flat collection</summary>
	/// <remarks>The collection only includes nodes with at least 1 child at binary tree levels 0, 4, 8, 12, etc.</remarks>
	readonly Dictionary<TreeNode, int> indices = new();
	/// <summary>Nodes in the serialised order including leaves; 16 values per node of the output tree.</summary>
	/// <remarks>Leaf-only nodes are not there, they are in <see cref="leavesData" /> collection</remarks>
	readonly List<object?> serialised = new();

	/// <summary>Around 2026-10-09, MaxMind heavily refactored their tree. It now includes thousands of nodes shared across IP blocks.<br/>
	/// This collection tracks nodes already serialised.</summary>
	/// <remarks>The collection only includes nodes at binary tree levels 0, 4, 8, 12, etc.</remarks>
	readonly HashSet<TreeNode> processedNodes = new();

	/// <summary>Payload data for leaf-only tree nodes</summary>
	readonly List<ulong> leavesData = new();
	/// <summary>No reason to store duplicate leaves; this hash maps unduplicates them.</summary>
	readonly Dictionary<ulong, int> leavesUndupe = new();
	/// <summary>Hash map to find indices of leaf nodes</summary>
	/// <remarks>The collection only includes child-free nodes at binary tree levels 0, 4, 8, 12, etc.</remarks>
	readonly Dictionary<TreeNode, int> leafIndices = new();

	// Each level of the output tree has exactly 16 children, indexed with 4 bit slices of the IP addresses
	const int width = 16;

	/// <summary>Recursively serialise the tree into BFS order; returns index of the root node passed in the argument.</summary>
	public int addTree( TreeNode root )
	{
		// Implementing breadth-first serialisation order; the list tracks the current level of the tree
		List<TreeNode> currentLevel = [ root ];
		while( currentLevel.Count > 0 )
		{
			// Collapse 4 levels of the binary tree starting from the currentLevel nodes into 16-wide nodes of the output tree
			int begin = serialised.Count;
			foreach( TreeNode node in currentLevel )
			{
				if( processedNodes.Contains( node ) )
				{
					// MaxMind has duplicated this node in the OG binary tree, both references are at the depth levels divisible by 4
					continue;
				}

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

				processedNodes.Add( node );
			}

			// Re-initialise currentLevel collection with all tree nodes added at the current level
			currentLevel.Clear();
			for( int i = begin; i < serialised.Count; i++ )
			{
				object? obj = serialised[ i ];
				if( null == obj )
					continue;
				if( obj is eRegion leaf )
					continue;
				// Duplicates in that list are harmless because the above loop uses a hash map anyway
				currentLevel.Add( (TreeNode)obj );
			}
		}

		// That line assumes the root has at least 1 child node, which should be reasonable
		return indices[ root ];
	}

	/// <summary>Recursive method to flatten 4 levels of binary tree into a single node of 16-wide tree</summary>
	/// <remarks>It appends objects to the <see cref="serialised" /> collection.</remarks>
	void addIntermediate( object? obj, int count )
	{
		Debug.Assert( BitOperations.IsPow2( count ) && count < width );
		if( null == obj || obj is eRegion )
		{
			// Replicate leaves and unassigned nodes into the width of the IP block being assembled
			for( int i = 0; i < count; i++ )
				serialised.Add( obj );
			return;
		}

		TreeNode node = (TreeNode)obj;
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
				continue;   // eRegion.Unassigned is 0
			if( obj is eRegion reg )
			{
				// Leaf node: insert the slice of 4 bits
				ulong val = (byte)reg;
				Debug.Assert( val < 0x10 );

				val <<= ( i * 4 );
				result |= val;
				continue;
			}
			if( obj is TreeNode )
			{
				// Non-leaf node: impossible to compress
				return null;
			}
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

			// Serialise these trees
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
							break;	// Normal node
						// Leaf-only node
						val = leafIndices[ node ] + nonLeafNodes;
						break;
					default:
						throw new ApplicationException( "Unexpected object type in the tree" );
				}

				// Store 4 bytes, slice 3: the output tree contains 24 bit integers
				Debug.Assert( val <= 0xFFFFFF );
				BinaryPrimitives.WriteInt32LittleEndian( span, val );
				span = span.Slice( 3 );
			}

			// Copy payload data of leaf-only nodes; List<ulong> already serialised, a single memcpy() will do
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