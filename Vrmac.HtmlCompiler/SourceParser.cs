using System.Diagnostics;
using System.Text;
namespace Vrmac;

/// <summary>Parses templates into a tree of objects derived from <see cref="Element" /></summary>
/// <remarks>Note the tree generally has multiple roots</remarks>
sealed class SourceParser
{
	readonly StringBuilder sb = new();
	sealed class BlockTemp
	{
		public readonly string name;
		public readonly List<Element> elements = new();
		public BlockTemp( string name ) => this.name = name;
		public Block build() => new Block( name, elements );
	}
	readonly Stack<BlockTemp> stack;
	readonly HashSet<string> blockNames = new();

	/// <summary>Create the object</summary>
	public SourceParser()
	{
		stack = new();
		stack.Push( new BlockTemp( string.Empty ) );
	}

	/// <summary>Mutable list of elements at the current level of the blocks tree</summary>
	List<Element> elements => stack.Peek().elements;

	void addText( bool isLast = false )
	{
		if( sb.Length == 0 )
			return;
		string text = sb.ToString();
		sb.Clear();

		if( isLast )
		{
			if( string.IsNullOrWhiteSpace( text ) )
				return;
			text = text.TrimEnd();
		}
		elements.Add( new Text( text ) );
	}

	/// <summary>End index of an identifier at the start of the span</summary>
	static int placeholderEnd( ReadOnlySpan<char> line )
	{
		for( int i = 0; i < line.Length; i++ )
		{
			char c = line[ i ];
			if( c.isAsciiLetter() )
				continue;
			if( i != 0 && c.isAsciiDigit() )
				continue;
			return i;
		}
		return -1;
	}

	/// <summary>Add template line which starts with `@`</summary>
	void addSingleLine( ReadOnlySpan<char> line )
	{
		addText();

		line = line.Trim();
		if( line[ 0 ] != '@' ) throw new ArgumentException();
		line = line.Slice( 1 );
		int idx = placeholderEnd( line );
		if( idx != 0 )
		{
			string name;
			if( idx >= 0 )
			{
				name = line.Slice( 0, idx ).str();
				throw new ApplicationException( $"`@{name}` element at the start of a line, no HTML allowed" );
			}
			name = line.str();
			elements.Add( new Placeholder( name ) );
			return;
		}

		if( line[ 0 ] == '{' )
		{
			line = line.Slice( 1 ).Trim();
			idx = placeholderEnd( line );
			if( idx >= 0 )
				throw new ArgumentException( "\"@{\" block marker should be followed with an identifier" );
			string name = line.str();
			if( string.IsNullOrWhiteSpace( name ) )
				throw new ArgumentException( "\"@{\" block marker should be followed with an identifier" );
			if( !blockNames.Add( name ) )
				throw new ArgumentException( "Multiple blocks with the same ID \"{0}\"", name );
			stack.Push( new BlockTemp( name ) );
			return;
		}

		if( line[ 0 ] == '}' )
		{
			if( line.Trim().Length != 1 )
				throw new ArgumentException( "\"@}\" block end marker should be the only thing on the line" );
			if( stack.Count <= 1 )
				throw new ArgumentException( "Too many closing block directives" );
			BlockTemp temp = stack.Pop();
			elements.Add( temp.build() );
			return;
		}

		throw new ArgumentException( "'@' character should be followed with an identifier, '{' or '}'" );
	}

	/// <summary>Add template line which contains both HTML markup and `@stuff` placeholders</summary>
	void addMixed( ReadOnlySpan<char> line, int idx )
	{
		sb.Append( indent );
		do
		{
			if( idx > 0 )
			{
				sb.Append( line.Slice( 0, idx ) );
				line = line.Slice( idx );
			}
			Debug.Assert( line[ 0 ] == '@' );
			line = line.Slice( 1 );
			if( line.IsEmpty )
				throw new ArgumentException( "'@' character at the end of a line" );

			idx = placeholderEnd( line );
			string name;
			if( idx < 0 )
				name = line.str();
			else if( idx > 0 )
			{
				name = line.Slice( 0, idx ).str();
				line = line.Slice( idx );
				idx = line.IndexOf( '@' );
			}
			else
				throw new ArgumentException( "'@' character should be followed with an identifier" );

			addText();
			elements.Add( new Placeholder( name ) );
		} while( idx >= 0 );
		sb.Append( line );
		if( !directives.singleLine )
			sb.Append( '\n' );
	}

	void parseDirective( ReadOnlySpan<char> line )
	{
		line = line.Trim();
		Debug.Assert( line.StartsWith( "@!" ) );
		line = line.Slice( 2 ).Trim();

		ReadOnlySpan<char> dir = findDirective( line, "route" );
		if( !dir.IsEmpty )
		{
			if( null != directives.route )
				throw new ArgumentException( "Multiple @!route directives" );
			directives.parseRoute( dir );
			return;
		}

		dir = findDirective( line, "indent" );
		if( !dir.IsEmpty )
		{
			if( directives.indent.HasValue )
				throw new ArgumentException( "Multiple @!indent directives" );
			if( !Compat.tryParse( dir, out int i ) )
				throw new ArgumentException( "Unable to parse indent integer" );
			directives.indent = i;
			return;
		}

		if( line.SequenceEqual( "singleLine" ) )
		{
			if( directives.singleLine )
				throw new ArgumentException( "Multiple @!singleLine directives" );
			directives.singleLine = true;
			return;
		}

		throw new NotImplementedException( $"Unsupported directive {line.str()}" );

		static ReadOnlySpan<char> findDirective( ReadOnlySpan<char> line, string tag )
		{
			if( !line.StartsWith( tag ) )
				return default;
			line = line.Slice( tag.Length );
			if( line.IsEmpty || !char.IsWhiteSpace( line[ 0 ] ) )
				throw new ArgumentException( $"Malformed @!{tag} directive" );
			line = line.Trim();
			if( line.IsEmpty )
				throw new ArgumentException( $"Malformed @!{tag} directive" );
			return line;
		}
	}

	/// <summary>Consume another line of the template</summary>
	public void addLine( ReadOnlySpan<char> line )
	{
		if( line.TrimStart().StartsWith( "@!" ) )
		{
			if( anyContent )
				throw new ArgumentException( "The \"@!\" directives must be at the start of the file before any content" );
			parseDirective( line );
			return;
		}

		if( !anyContent )
		{
			if( line.IsWhiteSpace() )
				return;
			indent = new string( '\t', directives.indent ?? 0 );
			anyContent = true;
		}

		int idx = line.IndexOf( '@' );
		if( idx < 0 )
		{
			sb.Append( indent );
			sb.Append( line );
			if( !directives.singleLine )
				sb.Append( '\n' );
			return;
		}

		if( line.TrimStart()[ 0 ] == '@' )
			addSingleLine( line );
		else
			addMixed( line, idx );
	}

	/// <summary>Finalise the parser, generate C# source codes.<br/>
	/// Returns stream with the UTF-8 encoded <c>*.cs</c> file</summary>
	public MemoryStream generate( string name, string ns, string template )
	{
		addText( true );
		if( stack.Count != 1 )
			throw new ArgumentException( "Missing block end marker" );

		MemoryStream ms = new();
		using StreamWriter w = new( ms, Encoding.UTF8, 1 << 12, true );
		Generator.generateSource( w, name, ns, template, elements, directives.route, directives.routeCached );
		ms.Seek( 0, SeekOrigin.Begin );
		return ms;
	}

	struct TemplateDirectives
	{
		public string? route { get; private set; }
		public bool routeCached { get; private set; }
		public void parseRoute( ReadOnlySpan<char> dir )
		{
			int idx = dir.IndexOfAny( ' ', '\t' );
			if( idx < 0 )
			{
				route = dir.str();
				routeCached = false;
				return;
			}
			if( idx == 0 )
				throw new ArgumentException();

			route = dir.Slice( 0, idx ).str();
			dir = dir.Slice( idx + 1 ).TrimStart();
			if( dir.SequenceEqual( "cached" ) )
			{
				routeCached = true;
				return;
			}

			string tail = dir.str();
			throw new ArgumentException( "Invalid route directive syntax" );
		}
		public int? indent;
		public bool singleLine;
	}

	bool anyContent = false;
	TemplateDirectives directives = default;
	string indent = string.Empty;
}