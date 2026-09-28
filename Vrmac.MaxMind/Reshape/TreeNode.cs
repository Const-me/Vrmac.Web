using System.Collections;
namespace Vrmac.MaxMind.Reshape;

sealed class TreeNode
{
	/// <summary>Node index in the OG tree</summary>
	public readonly int id;
	/// <summary>First child for bit = 0</summary>
	public object? left { get; private set; }
	/// <summary>Second child for bit = 1</summary>
	public object? right { get; private set; }

	public TreeNode( in ParseContext pc, int id )
	{
		this.id = id;
		pc.dictNodes.Add( id, this );

		left = pc.createNode( id, 0 );
		right = pc.createNode( id, 1 );
	}

	/// <summary>Compare two subtrees</summary>
	public bool equalLeaves( TreeNode that )
	{
		return equalChild( left, that.left ) && equalChild( right, that.right );
	}

	enum eChild: byte
	{
		Node,
		Leaf,
		Unassigned,
	}

	static eChild classify( object? obj )
	{
		if( null == obj )
			return eChild.Unassigned;
		if( obj is TreeNode )
			return eChild.Node;
		return eChild.Leaf;
	}

	static bool equalChild( object? a, object? b )
	{
		eChild ac = classify( a );
		eChild bc = classify( b );
		if( ac != bc )
			return false;
		switch( ac )
		{
			case eChild.Unassigned:
				return true;
			case eChild.Node:
				return ( (TreeNode)a! ).equalLeaves( (TreeNode)b! );
			case eChild.Leaf:
				return a == b;
			default:
				throw new ApplicationException();
		}
	}

	static object? transformNode( object? obj, BitArray processed, Func<object, object> tform )
	{
		if( null == obj )
			return null;
		if( obj is TreeNode node )
		{
			node.transformLeaves( processed, tform );
			return node;
		}
		return tform( obj );
	}

	static bool setBit( BitArray array, TreeNode node )
	{
		int index = node.id;
		if( array[ index ] )
			return false;
		array.Set( index, true );
		return true;
	}

	internal void transformLeaves( BitArray processed, Func<object, object> tform )
	{
		if( !setBit( processed, this ) )
			return;
		left = transformNode( left, processed, tform );
		right = transformNode( right, processed, tform );
	}

	internal object? collapseEqual( BitArray processed, ref int counter )
	{
		if( !setBit( processed, this ) )
			return this;

		if( left is TreeNode leftNode )
			left = leftNode.collapseEqual( processed, ref counter );
		if( right is TreeNode rightNode )
			right = rightNode.collapseEqual( processed, ref counter );

		eChild ac = classify( left );
		eChild bc = classify( right );
		if( ac != bc )
			return this;

		switch( ac )
		{
			case eChild.Unassigned:
				counter++;
				return null;
			case eChild.Leaf:
				if( Equals( left, right ) )
				{
					counter++;
					return left;
				}
				return this;
			default:
				return this;
		}
	}

	public TreeNode extractChildNode( bool r )
	{
		object? obj;
		if( r )
		{
			obj = right;
			right = null;
		}
		else
		{
			obj = left;
			left = null;
		}
		return (TreeNode)obj!;
	}

	IEnumerable<(TreeNode, int)> enumerate( int lvl )
	{
		yield return (this, lvl);
		if( left is TreeNode leftNode )
			foreach( var i in leftNode.enumerate( lvl + 1 ) )
				yield return i;
		if( right is TreeNode rightNode )
			foreach( var i in rightNode.enumerate( lvl + 1 ) )
				yield return i;
	}

	public IEnumerable<(TreeNode, int)> enumerate() => enumerate( 0 );

	object? select( bool bit ) => bit ? right : left;

	object? lookupImpl( IEnumerable<bool> bits )
	{
		TreeNode node = this;
		foreach( bool i in bits )
		{
			object? next = node.select( i );
			if( null == next )
				return null;
			if( next is TreeNode nextNode )
			{
				node = nextNode;
				continue;
			}
			return next;
		}
		return node;
	}

	public object? lookup( string ipString, bool mapto6 )
	{
		IPAddress ip = IPAddress.Parse( ipString );
		if( mapto6 )
			ip = ip.MapToIPv6();

		byte[] bytes = ip.GetAddressBytes();
		return lookupImpl( TreeData.enumBits( bytes ) );
	}

	public void removeAllNodes( BitArray processed, TreeNode node, ref int removed )
	{
		if( !setBit( processed, this ) )
			throw new ApplicationException( "Detected a tree cycle" );
		if( left is TreeNode leftNode )
		{
			if( leftNode != node )
				leftNode.removeAllNodes( processed, node, ref removed );
			else
			{
				left = null;
				removed++;
			}
		}

		if( right is TreeNode rightNode )
		{
			if( rightNode != node )
				rightNode.removeAllNodes( processed, node, ref removed );
			else
			{
				right = null;
				removed++;
			}
		}
	}
}