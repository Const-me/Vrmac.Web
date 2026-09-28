using System.Buffers;
using System.Diagnostics;
namespace Markdown;

/// <summary>Parse markdown into serialized tree representation</summary>
sealed class MarkdownParser
{
	/// <summary>Parse the markdown</summary>
	public static byte[] parse( ReadOnlySpan<byte> span )
	{
		if( span.Length >= 3 && span.Slice( 0, 3 ).SequenceEqual( byteOrderMark ) )
			span = span.Slice( 3 );

		MarkdownParser parser = new( false );
		parser.splitLines( span );
		return parser.toArray();
	}

	/// <summary>Parse the markdown, and keep the text of the first H1 section</summary>
	public static byte[] parse( ReadOnlySpan<byte> span, out ReadOnlyMemory<byte> firstH1 )
	{
		if( span.Length >= 3 && span.Slice( 0, 3 ).SequenceEqual( byteOrderMark ) )
			span = span.Slice( 3 );
		MarkdownParser parser = new( true );
		parser.splitLines( span );
		firstH1 = parser.builder.firstH1;
		return parser.toArray();
	}

	static ReadOnlySpan<byte> byteOrderMark => [ 0xEF, 0xBB, 0xBF ];

	private MarkdownParser( bool keepFirstH1 ) => builder = new( keepFirstH1 );
	MarkdownBuilder builder;
	byte[] toArray() => builder.toArray();

	void splitLines( ReadOnlySpan<byte> span )
	{
		while( !span.IsEmpty )
		{
			int idx = span.IndexOfAny( (byte)'\r', (byte)'\n' );
			if( idx < 0 )
			{
				parseLine( span );
				break;
			}

			parseLine( span.Slice( 0, idx ) );
			span = span.Slice( idx );
			int skip;
			if( span[ 0 ] == '\r' && span.Length >= 2 && span[ 1 ] == '\n' )
				skip = 2;
			else
				skip = 1;
			span = span.Slice( skip );
		}

		if( blockState == eBlockState.InParagraph )
			closeParagraph();
	}

	static ReadOnlySpan<byte> trimLeft( ReadOnlySpan<byte> span )
	{
		int idx = span.IndexOfAnyExcept( (byte)' ', (byte)'\t' );
		if( idx >= 0 )
			return span.Slice( idx );
		return default;
	}

	static ReadOnlySpan<byte> trimRight( ReadOnlySpan<byte> span )
	{
		int idx = span.LastIndexOfAnyExcept( (byte)' ', (byte)'\t' );
		if( idx >= 0 )
			return span.Slice( 0, idx + 1 );
		return default;
	}

	static ReadOnlySpan<byte> trim( ReadOnlySpan<byte> span ) =>
		trimRight( trimLeft( span ) );

	enum eBlockState: byte
	{
		/// <summary>Initial state</summary>
		None,
		/// <summary>Inside a paragraph</summary>
		InParagraph,
	}
	eBlockState blockState = eBlockState.None;

	void closeParagraph()
	{
		Debug.Assert( blockState == eBlockState.InParagraph );
		// If the last written thing was eBinaryElement.NewLine or LineBreak, remove the last byte from the list
		builder.trimLastNewline();

		// Close bold/italic/consolas if necessary
		while( spanStack.Count > 0 )
		{
			eBinaryElement elt = spanStack.Pop() switch
			{
				eSpanState.InBold => eBinaryElement.SpanBold,
				eSpanState.InItalic => eBinaryElement.SpanItalic,
				eSpanState.InConsolas => eBinaryElement.SpanConsolas,
				_ => throw new ApplicationException()
			};
			builder.closeElement( elt );
		}

		builder.closeElement( eBinaryElement.Paragraph );
		builder.element( eBinaryElement.NewLine );
		blockState = eBlockState.None;
	}

	void parseLine( ReadOnlySpan<byte> span )
	{
		span = trimLeft( span );
		if( span.IsEmpty )
		{
			if( blockState == eBlockState.InParagraph )
				closeParagraph();
			return;
		}

		if( span[ 0 ] == '#' )
		{
			parseHeader( span );
			return;
		}

		if( blockState == eBlockState.None )
		{
			builder.element( eBinaryElement.Paragraph );
			blockState = eBlockState.InParagraph;
		}
		parseContent( trimRight( span ) );
	}

	void parseHeader( ReadOnlySpan<byte> span )
	{
		if( blockState == eBlockState.InParagraph )
			closeParagraph();

		Debug.Assert( span[ 0 ] == '#' );
		int level = span.IndexOfAnyExcept( (byte)'#' );
		span = trim( span.Slice( level ) );
		level = Math.Min( level, 3 );

		eBinaryElement elt = (eBinaryElement)( (int)eBinaryElement.Heading1 + level - 1 );
		builder.headerText( elt, span );
		builder.element( elt );
		builder.text( span );
		builder.closeElement( elt );
		builder.element( eBinaryElement.NewLine );
	}

	enum eSpanState: byte
	{
		InBold,
		InItalic,
		InConsolas,
	}

	// Add field to class
	readonly Stack<eSpanState> spanStack = new();
	readonly SearchValues<byte> specialSearch = SearchValues.Create( (byte)'*', (byte)'`', (byte)'[', (byte)'\\' );

	void parseContent( ReadOnlySpan<byte> span )
	{
		Debug.Assert( blockState == eBlockState.InParagraph );

		// Check for line break at end: \ followed by nothing
		bool hasLineBreak = false;
		if( span.Length > 0 && span[ span.Length - 1 ] == (byte)'\\' )
		{
			hasLineBreak = true;
			span = span.Slice( 0, span.Length - 1 );
			span = trimRight( span );
		}

		while( !span.IsEmpty )
		{
			int nextSpecial = span.IndexOfAny( specialSearch );
			if( nextSpecial < 0 )
			{
				// No more special chars, emit rest as text
				builder.text( span );
				break;
			}

			// Emit text before special char
			if( nextSpecial > 0 )
				builder.text( span.Slice( 0, nextSpecial ) );

			// Process special character
			span = span.Slice( nextSpecial );
			byte b = span[ 0 ];
			if( b == (byte)'*' )
			{
				// Check for ** (bold) vs * (italic)
				if( span.Length > 1 && span[ 1 ] == (byte)'*' )
				{
					// Bold
					if( spanStack.Count > 0 && spanStack.Peek() == eSpanState.InBold )
					{
						builder.closeElement( eBinaryElement.SpanBold );
						spanStack.Pop();
					}
					else
					{
						builder.element( eBinaryElement.SpanBold );
						spanStack.Push( eSpanState.InBold );
					}
					span = span.Slice( 2 );
				}
				else
				{
					// Italic
					if( spanStack.Count > 0 && spanStack.Peek() == eSpanState.InItalic )
					{
						builder.closeElement( eBinaryElement.SpanItalic );
						spanStack.Pop();
					}
					else
					{
						builder.element( eBinaryElement.SpanItalic );
						spanStack.Push( eSpanState.InItalic );
					}
					span = span.Slice( 1 );
				}
			}
			else if( b == (byte)'`' )
			{
				// Consolas span
				if( spanStack.Count > 0 && spanStack.Peek() == eSpanState.InConsolas )
				{
					builder.closeElement( eBinaryElement.SpanConsolas );
					spanStack.Pop();
				}
				else
				{
					builder.element( eBinaryElement.SpanConsolas );
					spanStack.Push( eSpanState.InConsolas );
				}
				span = span.Slice( 1 );
			}
			else if( b == (byte)'[' )
			{
				// Start of hyperlink
				span = parseHyperlink( span );
			}
			else if( b == (byte)'\\' )
			{
				// Escape next character (emit it literally)
				span = span.Slice( 1 );
				if( !span.IsEmpty )
				{
					builder.text( span.Slice( 0, 1 ) );
					span = span.Slice( 1 );
				}
			}
		}

		if( hasLineBreak )
			builder.element( eBinaryElement.LineBreak );
		else
			builder.element( eBinaryElement.NewLine );
	}

	ReadOnlySpan<byte> parseHyperlink( ReadOnlySpan<byte> span )
	{
		Debug.Assert( span[ 0 ] == (byte)'[' );
		span = span.Slice( 1 );

		// Find closing ]
		int closeBracket = span.IndexOf( (byte)']' );
		if( closeBracket < 0 )
			throw new InvalidOperationException( "Unclosed link text: missing ]" );

		ReadOnlySpan<byte> linkText = span.Slice( 0, closeBracket );
		span = span.Slice( closeBracket + 1 );

		// Check for opening (
		if( span.IsEmpty || span[ 0 ] != (byte)'(' )
			throw new InvalidOperationException( "Invalid link: expected ( after ]" );

		span = span.Slice( 1 );

		// Find closing )
		int closeParen = span.IndexOf( (byte)')' );
		if( closeParen < 0 )
			throw new InvalidOperationException( "Unclosed link URL: missing )" );

		ReadOnlySpan<byte> linkUrl = span.Slice( 0, closeParen );
		span = span.Slice( closeParen + 1 );

		// Emit hyperlink
		builder.element( eBinaryElement.Hyperlink );
		builder.text( linkUrl );  // URL first
		builder.text( linkText ); // Then text
		builder.closeElement( eBinaryElement.Hyperlink );

		return span;
	}
}