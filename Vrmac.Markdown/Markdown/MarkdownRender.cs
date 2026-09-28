namespace Markdown;

static class MarkdownRender
{
	/// <summary>Convert UTF-8 markdown into UTF-8 HTML</summary>
	public static byte[] render( byte[] bytes, int indent = 1 )
	{
		bytes = MarkdownParser.parse( bytes );
		List<byte> list = new();
		using( var w = new Html( list, indent ) )
			BinaryParser.parse( w, bytes );
		return list.ToArray();
	}

	internal static ReadOnlySpan<byte> divOpen => "<div class=\"markdown\">"u8;
	internal static ReadOnlySpan<byte> divClose => "</div>"u8;
	static ReadOnlySpan<byte> br => "<br>"u8;

	static ReadOnlySpan<byte> boldOpen => "<b>"u8;
	static ReadOnlySpan<byte> boldClose => "</b>"u8;
	static ReadOnlySpan<byte> italicOpen => "<i>"u8;
	static ReadOnlySpan<byte> italicClose => "</i>"u8;
	static ReadOnlySpan<byte> codeOpen => "<code>"u8;
	static ReadOnlySpan<byte> codeClose => "</code>"u8;

	static ReadOnlySpan<byte> pNorm => "p>"u8;
	static ReadOnlySpan<byte> h1 => "h1>"u8;
	static ReadOnlySpan<byte> h2 => "h2>"u8;
	static ReadOnlySpan<byte> h3 => "h3>"u8;

	static ReadOnlySpan<byte> link1 => "<a href=\""u8;
	static ReadOnlySpan<byte> link2 => "\">"u8;
	static ReadOnlySpan<byte> linkEnd => "</a>"u8;

	internal sealed class Html: iMarkdownSink, IDisposable
	{
		readonly List<byte> bytes;
		readonly byte[] m_indent;
		bool nextLine;

		public Html( List<byte> bytes, int indent )
		{
			this.bytes = bytes;
			m_indent = new byte[ indent + 1 ];
			Span<byte> span = m_indent;
			span.Fill( (byte)'\t' );
			bytes.AddRange( span.Slice( 1 ) );
			bytes.AddRange( divOpen );
			bytes.Add( (byte)'\n' );
			nextLine = true;
		}

		public void Dispose()
		{
			if( !nextLine )
				bytes.Add( (byte)'\n' );
			ReadOnlySpan<byte> span = m_indent;
			bytes.AddRange( span.Slice( 1 ) );
			bytes.AddRange( divClose );
			bytes.Add( (byte)'\n' );
		}

		void indent()
		{
			if( nextLine )
			{
				bytes.AddRange( m_indent );
				nextLine = false;
			}
		}

		void iMarkdownSink.text( ReadOnlySpan<byte> span )
		{
			indent();
			bytes.AddRange( span );
		}
		void iMarkdownSink.newLine()
		{
			bytes.Add( (byte)'\n' );
			nextLine = true;
		}
		void iMarkdownSink.lineBreak()
		{
			indent();
			bytes.AddRange( br );
		}
		void iMarkdownSink.span( eTextSpan style, bool close )
		{
			indent();
			ReadOnlySpan<byte> span = (style, close) switch
			{
				(eTextSpan.Bold, false ) => boldOpen,
				(eTextSpan.Bold, true ) => boldClose,
				(eTextSpan.Italic, false ) => italicOpen,
				(eTextSpan.Italic, true ) => italicClose,
				(eTextSpan.Consolas, false ) => codeOpen,
				(eTextSpan.Consolas, true ) => codeClose,
				_ => throw new NotSupportedException(),
			};
			bytes.AddRange( span );
		}

		void iMarkdownSink.para( eParagraph style, bool close )
		{
			indent();
			bytes.Add( (byte)'<' );
			if( close )
				bytes.Add( (byte)'/' );
			ReadOnlySpan<byte> span = style switch
			{
				eParagraph.Normal => pNorm,
				eParagraph.H1 => h1,
				eParagraph.H2 => h2,
				eParagraph.H3 => h3,
				_ => throw new NotSupportedException(),
			};
			bytes.AddRange( span );
		}
		void iMarkdownSink.linkBegin( ReadOnlySpan<byte> url )
		{
			indent();
			bytes.AddRange( link1 );
			bytes.AddRange( url );
			bytes.AddRange( link2 );
		}

		void iMarkdownSink.linkEnd() => bytes.AddRange( linkEnd );
	}
}