namespace Vrmac;

/// <summary>Utility function to render markdown into HTML using lightweight custom parser</summary>
public static class MarkdownRender
{
	/// <summary>Convert markdown to HTML</summary>
	public static byte[] render( ReadOnlyMemory<byte> markdown )
	{
		byte[] bytes = Markdown.MarkdownParser.parse( markdown.Span );
		List<byte> list = new();
		using( Markdown.MarkdownRender.Html w = new( list, 1 ) )
			Markdown.BinaryParser.parse( w, bytes );
		return list.ToArray();
	}

	/// <summary>Convert markdown to HTML, and extract the first H1 header text</summary>
	public static byte[] render( ReadOnlyMemory<byte> markdown, int indent,
		out ReadOnlyMemory<byte> firstHeader1 )
	{
		byte[] bytes = Markdown.MarkdownParser.parse( markdown.Span, out firstHeader1 );
		// Convert binary tree into UTF-8 markup
		List<byte> list = new();
		using( Markdown.MarkdownRender.Html w = new( list, indent ) )
			Markdown.BinaryParser.parse( w, bytes );
		return list.ToArray();
	}
}