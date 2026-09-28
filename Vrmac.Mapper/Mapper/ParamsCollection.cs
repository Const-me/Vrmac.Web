using MySqlConnector;
using System.Collections;
namespace Vrmac.Mapper;

/// <summary>Utility structure to return new array of query parameters using collection expression without GC allocations</summary>
public struct ParamsCollection: IEnumerable<object?>, IDisposable
{
	[ThreadStatic]
	static MySqlParameter[]? destinationStatic;
	internal static void setDestination( MySqlParameter[] arr ) => destinationStatic = arr;
	/// <summary>Reserved for internal use</summary>
	public void Dispose() => destinationStatic = null;

	internal void checkExpectedLength()
	{
		if( rdi == arr.Length )
			return;
		string expected = arr.Length.pluralString( "parameter" );
		throw new ArgumentException( $"iNamedParameters.values() was supposed to store {expected}, stored {rdi}" );
	}

	readonly MySqlParameter[] arr;
	int rdi;
	/// <summary>Reserved for internal use by the compiler</summary>
	public ParamsCollection()
	{
		arr = destinationStatic ??
			throw new InvalidOperationException( "ParamsCollection should only be constructed by the mapper infrastructure" );
		rdi = 0;
	}

	/// <summary>Called by compiler for <c>[ a, b, c ]</c> expressions</summary>
	public void Add( object? item )
	{
		arr[ rdi ].Value = item;
		rdi++;
	}

	// Microsoft neglected to document that, but without the following crap which is never called the compiler fails the collection expressions
	IEnumerator<object?> enumerate()
	{
		for( int i = 0; i < rdi; i++ )
			yield return arr[ i ]?.Value;
	}
	IEnumerator<object?> IEnumerable<object?>.GetEnumerator() => enumerate();
	IEnumerator IEnumerable.GetEnumerator() => enumerate();
}