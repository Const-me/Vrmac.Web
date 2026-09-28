using System.Collections;
namespace Vrmac.MaxMind.Reshape;

sealed class TreeData
{
	public TreeData( ReadOnlySpan<byte> data, int countNodes, IReadOnlyDictionary<long, Record> leaves )
	{
		ParseContext pc = new( data, countNodes, leaves );
		root = new TreeNode( pc, 0 );
		processed = new BitArray( countNodes );
	}

	public readonly TreeNode root;
	/// <summary>Conceptually an unordered set of tree nodes, but way more efficient</summary>
	readonly BitArray processed;

	TreeNode findNode( IEnumerable<bool> bits )
	{
		TreeNode node = root;
		foreach( bool bit in bits )
		{
			object? that = bit ? node.right : node.left;
			node = (TreeNode)that!;
		}
		return node;
	}

	internal static IEnumerable<bool> enumBits( byte[] arr )
	{
		for( int i = 0; i < arr.Length; i++ )
		{
			byte e = arr[ i ];
			for( byte bit = 0x80; bit != 0; bit >>= 1 )
			{
				bool res = 0 != ( e & bit );
				yield return res;
			}
		}
	}

	public TreeNode findNode( byte[] arr ) => findNode( enumBits( arr ) );

	public TreeNode extractNode( byte[] arr )
	{
		IEnumerable<bool> bits = enumBits( arr );
		int lastBitIndex = arr.Length * 8 - 1;
		TreeNode parent = findNode( bits.Take( lastBitIndex ) );

		bool lastChild = ( arr[ arr.Length - 1 ] & 1 ) != 0;
		return parent.extractChildNode( lastChild );
	}

	public void extractNode( byte[] arr, TreeNode expected )
	{
		TreeNode ext = extractNode( arr );
		if( ReferenceEquals( ext, expected ) )
			return;
		throw new ApplicationException( "extractNode - unexpected one" );
	}

	public void transformLeaves( Func<object, object> tform )
	{
		processed.SetAll( false );
		root.transformLeaves( processed, tform );
	}

	public int collapseEqual()
	{
		processed.SetAll( false );
		int collapsed = 0;
		root.collapseEqual( processed, ref collapsed );
		return collapsed;
	}

	public int removeAllNodes( TreeNode node )
	{
		int result = 0;
		processed.SetAll( false );
		root.removeAllNodes( processed, node, ref result );
		return result;
	}
}