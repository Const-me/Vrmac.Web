using System.Text;
namespace Vrmac;

static class Compat
{
	public static bool isAsciiLetter( this char c )
	{
		unchecked
		{
			return (uint)( ( c | 0x20 ) - 'a' ) <= 'z' - 'a';
		}
	}

	static bool isBetween( char c, char minInclusive, char maxInclusive )
	{
		unchecked
		{
			return (uint)( c - minInclusive ) <= (uint)( maxInclusive - minInclusive );
		}
	}

	public static bool isAsciiDigit( this char c ) => isBetween( c, '0', '9' );

	public static unsafe string str( this ReadOnlySpan<char> span )
	{
		if( span.IsEmpty )
			return string.Empty;
		fixed( char* rsi = span )
			return new string( rsi, 0, span.Length );
	}

	public static unsafe void Append( this StringBuilder sb, ReadOnlySpan<char> span )
	{
		if( span.IsEmpty )
			return;
		fixed( char* rsi = span )
			sb.Append( rsi, span.Length );
	}

	public static void AppendJoin( this StringBuilder sb, string separator, IEnumerable<string> list )
	{
		bool first = true;
		foreach( string s in list )
		{
			if( first )
				first = false;
			else
				sb.Append( separator );
			sb.Append( s );
		}
	}

	public static bool tryParse( ReadOnlySpan<char> span, out int i ) =>
		int.TryParse( span.str(), out i );

	public static string relativePath( string relativeTo, string path )
	{
		if( path.StartsWith( relativeTo, StringComparison.OrdinalIgnoreCase ) )
			return path.Substring( relativeTo.Length );
		throw new ArgumentException( "Linked HTML files are not supported" );
	}
}