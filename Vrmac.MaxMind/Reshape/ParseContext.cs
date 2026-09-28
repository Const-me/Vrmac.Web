using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
namespace Vrmac.MaxMind.Reshape;

readonly ref struct ParseContext
{
	readonly ReadOnlySpan<byte> data;
	readonly int countNodes;
	readonly IReadOnlyDictionary<long, Record> leaves;
	public readonly Dictionary<int, TreeNode> dictNodes;
	public ParseContext( ReadOnlySpan<byte> data, int countNodes, IReadOnlyDictionary<long, Record> leaves )
	{
		this.data = data;
		this.countNodes = countNodes;
		this.leaves = leaves;
		dictNodes = new();
	}

	public object? createNode( int id, int leafIndex )
	{
		int node = readNode( data, id, leafIndex );
		if( node == countNodes )
			return null;
		if( node > countNodes )
			return leaves[ node ];

		if( dictNodes.TryGetValue( node, out var tn ) )
			return tn;
		return new TreeNode( this, node );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static int readNode( ReadOnlySpan<byte> tree, int nodeNumber, int leafIndex )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( leafIndex );
		ArgumentOutOfRangeException.ThrowIfGreaterThan( leafIndex, 1 );

		int rsi = nodeNumber * 6;
		rsi += leafIndex * 3;
		uint e = MemoryMarshal.Read<uint>( tree.Slice( rsi, 4 ) );
		e = Bmi2.ZeroHighBits( e, 24 );
		return unchecked((int)e);
	}
}