using System.Diagnostics;
using Vrmac;
namespace Markdown;

static class BinaryParser
{
	public static void parse( iMarkdownSink sink, ReadOnlySpan<byte> span )
	{
		while( !span.IsEmpty )
		{
			byte tag = span[ 0 ];
			span = span.Slice( 1 );

			if( tag < 0x80 )
			{
				span = parseText( span, tag, out ReadOnlySpan<byte> text );
				sink.text( text );
			}
			else
			{
				// Something else
				bool closingTag = ( 0 != ( tag & (byte)eBinaryElement.ClosingTag ) );
				eBinaryElement elt = (eBinaryElement)( tag & ~(byte)eBinaryElement.ClosingTag );
				span = parseElement( sink, span, elt, closingTag );
			}
		}
	}

	static ReadOnlySpan<byte> parseText( ReadOnlySpan<byte> span, byte tag, out ReadOnlySpan<byte> text )
	{
		if( tag < 0x7F )
		{
			// Short text
			text = span.Slice( 0, tag );
			return span.Slice( tag );
		}

		if( tag == 0x7F )
		{
			// Long text
			int cbLength;
			int cbText = MultiByte.parse( span, out cbLength );
			cbText += 0x7F;
			span = span.Slice( cbLength );
			text = span.Slice( 0, cbText );
			return span.Slice( cbText );
		}

		throw new ArgumentException();
	}

	static ReadOnlySpan<byte> parseElement( iMarkdownSink sink, ReadOnlySpan<byte> span, eBinaryElement elt, bool closingTag )
	{
		Debug.Assert( Enum.IsDefined( elt ) );

		switch( elt )
		{
			case eBinaryElement.NewLine:
				Debug.Assert( !closingTag );
				sink.newLine();
				return span;
			case eBinaryElement.LineBreak:
				Debug.Assert( !closingTag );
				sink.lineBreak();
				sink.newLine();
				return span;

			case eBinaryElement.SpanBold:
				sink.span( eTextSpan.Bold, closingTag );
				return span;
			case eBinaryElement.SpanItalic:
				sink.span( eTextSpan.Italic, closingTag );
				return span;
			case eBinaryElement.SpanConsolas:
				sink.span( eTextSpan.Consolas, closingTag );
				return span;

			case eBinaryElement.Paragraph:
				sink.para( eParagraph.Normal, closingTag );
				return span;
			case eBinaryElement.Heading1:
				sink.para( eParagraph.H1, closingTag );
				return span;
			case eBinaryElement.Heading2:
				sink.para( eParagraph.H2, closingTag );
				return span;
			case eBinaryElement.Heading3:
				sink.para( eParagraph.H3, closingTag );
				return span;
		}

		if( elt == eBinaryElement.Hyperlink )
		{
			if( closingTag )
			{
				sink.linkEnd();
				return span;
			}

			byte tag = span[ 0 ];
			span = parseText( span.Slice( 1 ), tag, out ReadOnlySpan<byte> url );
			sink.linkBegin( url );
			return span;
		}

		throw new NotImplementedException();
	}
}