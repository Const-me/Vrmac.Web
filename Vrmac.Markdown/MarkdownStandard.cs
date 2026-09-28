using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Buffers;
using System.Text;
using Vrmac.Html;
namespace Vrmac;

/// <summary>Utility function to render markdown into HTML using extremely inefficient but standard-complaint parser,
/// from a crappy third-party library</summary>
public static class MarkdownStandard
{
	/// <summary>Convert markdown to HTML, and extract the first H1 header text</summary>
	public static byte[] render( string path, int indent, bool longContent,
		out ReadOnlyMemory<byte> firstHeader1, iMarkdownPlaceholders? placeholders = null )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( indent );

		string markdown = File.ReadAllText( path );
		MarkdownPipeline pipeline;
		if( null == placeholders )
			pipeline = longContent ? pipelineLong : pipelineDefault;
		else
			pipeline = pipelineWithPlaceholders;
		MarkdownDocument document = Markdig.Markdown.Parse( markdown, pipeline );

		// Extract first H1 header text
		firstHeader1 = extractFirstH1( document );

		// Render to HTML with indentation and outer container
		return renderWithIndent( pipeline, document, indent, placeholders );
	}

	static MarkdownStandard()
	{
		pipelineDefault = buildPipeline( false );
		pipelineLong = buildPipeline( true );
		pipelineWithPlaceholders = buildPlaceholdersPipeline();
	}

	static MarkdownPipeline buildPipeline( bool longContent )
	{
		MarkdownPipelineBuilder builder = new();
		builder.UseListExtras();
		if( longContent )
			builder.UseFootnotes();
		builder.UseGenericAttributes();
		return builder.Build();
	}

	static MarkdownPipeline buildPlaceholdersPipeline()
	{
		MarkdownPipelineBuilder builder = new();
		builder.UseListExtras();
		builder.UseGenericAttributes();
		builder.Use<Placeholders.PlaceholderExtension>();
		return builder.Build();
	}

	static readonly MarkdownPipeline pipelineDefault, pipelineLong, pipelineWithPlaceholders;

	static ReadOnlyMemory<byte> extractFirstH1( MarkdownDocument document )
	{
		HeadingBlock? h1 = document.Descendants<HeadingBlock>()
			.FirstOrDefault( h => h.Level == 1 );
		if( h1?.Inline is null )
			return ReadOnlyMemory<byte>.Empty;

		// Collect all literal text within the heading's inline children
		var sb = new StringBuilder();
		foreach( var inline in h1.Inline.Descendants<LiteralInline>() )
			sb.Append( inline.Content.ToString() );
		return Encoding.UTF8.GetBytes( sb.ToString() );
	}

	static byte[] renderWithIndent( MarkdownPipeline pipeline, MarkdownDocument document, int indentLevel, iMarkdownPlaceholders? placeholders )
	{
		MemoryStream ms = CachedMemStream.threadLocal();

		using( StreamWriter writer = new( ms, Encoding.UTF8, 2048, true ) )
		{
			HtmlRenderer renderer = new( writer );
			if( null != placeholders )
				renderer.ObjectRenderers.Insert( 0, new Placeholders.PlaceholderRenderer( placeholders ) );
			pipeline.Setup( renderer );
			renderer.Render( document );
		}

		int writtenBytes = (int)ms.Length;
		if( writtenBytes <= 0 )
			return Array.Empty<byte>();

		ArrayPool<byte> pool = ArrayPool<byte>.Shared;
		byte[] arr = pool.Rent( writtenBytes );
		try
		{
			ms.Seek( 0, SeekOrigin.Begin );
			ms.ReadExactly( arr.AsSpan( 0, writtenBytes ) );

			ms.SetLength( 0 );
			bool first = true;
			Span<byte> indent = stackalloc byte[ indentLevel + 1 ];
			indent.Fill( (byte)'\t' );

			foreach( ReadOnlyMemory<byte> line in LineParser.parse( arr.AsMemory( 0, writtenBytes ) ) )
			{
				if( line.IsEmpty )
					continue;

				if( first )
				{
					ms.write( indent.Slice( 1 ) );
					ms.Write( Markdown.MarkdownRender.divOpen );
					ms.newLine();
					first = false;
				}
				ms.write( indent );
				ms.write( line );
				ms.newLine();
			}

			if( !first )
			{
				ms.write( indent.Slice( 1 ) );
				ms.Write( Markdown.MarkdownRender.divClose );
			}

			return ms.ToArray();
		}
		finally
		{
			pool.Return( arr );
		}
	}
}