using Markdig;
using Markdig.Renderers;
namespace Vrmac.Placeholders;

sealed class PlaceholderExtension: IMarkdownExtension
{
	public void Setup( MarkdownPipelineBuilder pipeline ) =>
		pipeline.BlockParsers.Insert( 0, new PlaceholderParser() );
	public void Setup( MarkdownPipeline pipeline, IMarkdownRenderer renderer ) { }
}