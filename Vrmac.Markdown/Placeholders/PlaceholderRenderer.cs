using Markdig.Renderers;
using Markdig.Renderers.Html;
namespace Vrmac.Placeholders;

sealed class PlaceholderRenderer: HtmlObjectRenderer<PlaceholderBlock>
{
	readonly iMarkdownPlaceholders callback;
	public PlaceholderRenderer( iMarkdownPlaceholders callback ) => this.callback = callback;
	protected override void Write( HtmlRenderer renderer, PlaceholderBlock obj ) =>
		callback.write( (StreamWriter)renderer.Writer, obj.id );
}