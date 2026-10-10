using System.Collections;
namespace Vrmac.MaxMind.Reshape;

sealed class SharedNodesDetector
{
	/// <summary>Detect and print junction points in the binary tree</summary>
	public static void detect( TreeData tree )
	{
		SharedNodesDetector obj = new( tree.countNodes );
		int remaps = obj.findRemaps( tree.root );
		if( remaps <= 0 )
		{
			Console.WriteLine( "The graph does not contain any cycles" );
			return;
		}
		obj.listRemaps( tree.root );

		int count = obj.blocks.Count;
		int i = 0;
		foreach( JunctionPoint jp in obj.blocks.Values )
		{
			i++;
			Console.WriteLine( "--- Block {0} of {1}: {2} child nodes, {3} child leaves", i, count, jp.childNodes, jp.childLeaves );
			foreach( string ip in jp.blocks )
				Console.WriteLine( ip );
		}
	}

	readonly BitArray bitmap;
	readonly Dictionary<int, JunctionPoint> blocks;

	sealed class JunctionPoint
	{
		public List<string> blocks { get; } = new();
		public int childNodes { get; private set; }
		public int childLeaves { get; private set; }

		public void setChildren( in SubtreeStats ss )
		{
			childNodes = ss.nodes;
			childLeaves = ss.leaves;
		}
	}

	SharedNodesDetector( int countNodes )
	{
		bitmap = new BitArray( countNodes );
		blocks = new Dictionary<int, JunctionPoint>();
	}

	static bool setBit( BitArray array, TreeNode node )
	{
		int index = node.id;
		if( array[ index ] )
			return false;
		array.Set( index, true );
		return true;
	}

	int findRemaps( TreeNode root )
	{
		bitmap.SetAll( false );
		blocks.Clear();
		impl( root );
		return blocks.Count();

		void impl( TreeNode node )
		{
			if( setBit( bitmap, node ) )
			{
				if( node.left is TreeNode leftNode )
					impl( leftNode );
				if( node.right is TreeNode rightNode )
					impl( rightNode );
				return;
			}
			ref JunctionPoint? jp = ref CollectionsMarshal.GetValueRefOrAddDefault( blocks, node.id, out _ );
			jp ??= new JunctionPoint();
		}
	}

	void listRemaps( TreeNode root )
	{
		bitmap.SetAll( false );
		BitStack stack = new();
		visitNode( root );

		SubtreeStats visitNode( TreeNode node )
		{
			JunctionPoint? junction;
			if( blocks.TryGetValue( node.id, out junction ) )
				junction.blocks.Add( stack.ToString() );

			SubtreeStats res = default;
			if( !setBit( bitmap, node ) )
				return res;

			res += visitChild( node.left, false );
			res += visitChild( node.right, true );
			junction?.setChildren( res );
			return res;
		}

		SubtreeStats visitChild( object? obj, bool bit )
		{
			if( obj is TreeNode node )
			{
				using var lvl = stack.push( bit );
				return visitNode( node ).addNode();
			}
			return SubtreeStats.leaf( obj );
		}
	}

	readonly struct SubtreeStats
	{
		public readonly int nodes, leaves;
		SubtreeStats( int nodes, int leaves )
		{
			this.nodes = nodes;
			this.leaves = leaves;
		}
		public SubtreeStats addNode() => new SubtreeStats( nodes + 1, leaves );
		public static SubtreeStats leaf( object? obj )
		{
			Debug.Assert( !( obj is TreeNode ) );
			return new SubtreeStats( 0, ( null != obj ) ? 1 : 0 );
		}
		public static SubtreeStats operator +( in SubtreeStats a, in SubtreeStats b ) =>
			new SubtreeStats( a.nodes + b.nodes, a.leaves + b.leaves );
	}
}