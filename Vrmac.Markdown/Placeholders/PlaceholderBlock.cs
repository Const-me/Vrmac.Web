using Markdig.Parsers;
using Markdig.Syntax;
namespace Vrmac.Placeholders;

sealed class PlaceholderBlock: LeafBlock
{
	public PlaceholderBlock( BlockParser? parser, string id ) : base( parser ) =>
		this.id = id;
	public string id { get; }
}