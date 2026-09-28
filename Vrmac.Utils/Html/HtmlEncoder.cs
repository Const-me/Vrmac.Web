using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
namespace Vrmac.Html;

/// <summary>Better replacement of WebUtility.HtmlEncode which produces UTF-8 bytes</summary>
public static class HtmlEncoder
{
	static readonly SearchValues<char> entityChars = SearchValues.Create( "\"&\'<>" );

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	static int indexOfEntity( ReadOnlySpan<char> str ) => str.IndexOfAny( entityChars );

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	static int encodedLength( char c ) => c switch
	{
		'\"' => 6, // &quot;
		'&' => 5, // &amp;
		'\'' => 5, // &#39;
		'<' => 4, // &lt;
		'>' => 4, // &gt;
		_ => throw new ApplicationException(),
	};

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	static ReadOnlySpan<char> encodedEntity( char c ) => c switch
	{
		'\"' => "&quot;",
		'&' => "&amp;",
		'\'' => "&#39;",
		'<' => "&lt;",
		'>' => "&gt;",
		_ => throw new ApplicationException(),
	};

	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static int lengthChars( ReadOnlySpan<char> str, int idx )
	{
		int res = 0;
		while( true )
		{
			res += encodedLength( str[ idx ] ) + idx;
			str = str.Slice( idx + 1 );
			idx = indexOfEntity( str );
			if( idx < 0 )
				return res + str.Length;
		}
	}

	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	static void encodeChars( Span<char> dest, ReadOnlySpan<char> source, int idx )
	{
		while( true )
		{
			source.Slice( 0, idx ).CopyTo( dest );
			source = source.Slice( idx );
			dest = dest.Slice( idx );

			ReadOnlySpan<char> ent = encodedEntity( source[ 0 ] );
			source = source.Slice( 1 );
			ent.CopyTo( dest );
			dest = dest.Slice( ent.Length );

			idx = indexOfEntity( source );
			if( idx < 0 )
			{
				Debug.Assert( source.Length == dest.Length );
				source.CopyTo( dest );
				return;
			}
		}
	}

	/// <summary>Write unsafe HTML string into stream of UTF-8 bytes</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public static void write( Stream stream, ReadOnlySpan<char> str )
	{
		int idx = indexOfEntity( str );
		if( idx < 0 )
		{
			stream.writeRawText( str );
			return;
		}

		int cc = lengthChars( str, idx );
		const int maxStackChars = 1024;
		if( cc <= maxStackChars )
		{
			Span<char> charsSpan = stackalloc char[ cc ];
			encodeChars( charsSpan, str, idx );
			stream.writeRawText( charsSpan );
		}
		else
		{
			ArrayPool<char> pool = ArrayPool<char>.Shared;
			char[] arr = pool.Rent( cc );
			try
			{
				Span<char> charsSpan = arr.AsSpan( 0, cc );
				encodeChars( charsSpan, str, idx );
				stream.writeRawText( charsSpan );
			}
			finally
			{
				pool.Return( arr );
			}
		}
	}

	/// <summary>Utility function to encode text into UTF-8</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	static byte[] encodedBytes( ReadOnlySpan<char> chars )
	{
		Encoding enc = Encoding.UTF8;
		int cb = enc.GetByteCount( chars );
		if( 0 != cb )
		{
			byte[] arr = new byte[ cb ];
			enc.GetBytes( chars, arr );
			return arr;
		}
		return Array.Empty<byte>();
	}

	/// <summary>Convert unsafe HTML string into newly allocated array of UTF-8 bytes</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public static byte[] encode( ReadOnlySpan<char> str )
	{
		int idx = indexOfEntity( str );
		if( idx < 0 )
			return encodedBytes( str );

		int cc = lengthChars( str, idx );
		const int maxStackChars = 1024;

		if( cc <= maxStackChars )
		{
			Span<char> charsSpan = stackalloc char[ cc ];
			encodeChars( charsSpan, str, idx );
			return encodedBytes( charsSpan );
		}
		else
		{
			ArrayPool<char> pool = ArrayPool<char>.Shared;
			char[] arr = pool.Rent( cc );
			try
			{
				Span<char> charsSpan = arr.AsSpan( 0, cc );
				encodeChars( charsSpan, str, idx );
				return encodedBytes( charsSpan );
			}
			finally
			{
				pool.Return( arr );
			}
		}
	}
}