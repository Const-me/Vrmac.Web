namespace Vrmac;

/// <summary>Utility functions to concatenate spans of bytes</summary>
public static class Spans
{
	/// <summary>Concatenate 2 spans of bytes into a new heap allocated array</summary>
	public static byte[] concat( ReadOnlySpan<byte> a, ReadOnlySpan<byte> b )
	{
		byte[] arr = new byte[ a.Length + b.Length ];
		Span<byte> span = arr;

		a.CopyTo( span );
		span = span.Slice( a.Length );

		Debug.Assert( b.Length == span.Length );
		b.CopyTo( span );
		return arr;
	}

	/// <summary>Concatenate 3 spans of bytes into a new heap allocated array</summary>
	public static byte[] concat( ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c )
	{
		byte[] arr = new byte[ a.Length + b.Length + c.Length ];
		Span<byte> span = arr;

		a.CopyTo( span );
		span = span.Slice( a.Length );

		b.CopyTo( span );
		span = span.Slice( b.Length );

		Debug.Assert( c.Length == span.Length );
		c.CopyTo( span );
		return arr;
	}
}