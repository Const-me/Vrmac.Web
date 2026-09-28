using Markdig.Parsers;
using System.Text.RegularExpressions;
namespace Vrmac.Placeholders;

sealed partial class PlaceholderParser: BlockParser
{
	public PlaceholderParser() => OpeningCharacters = [ '@' ];

	[GeneratedRegex( @"^@([a-zA-Z][a-zA-Z0-9]*)$" )]
	public static partial Regex markerRegex();

	public override BlockState TryOpen( BlockProcessor processor )
	{
		// Don't match if indented as code (4+ spaces)
		if( processor.IsCodeIndent )
			return BlockState.None;

		ReadOnlySpan<char> line = processor.Line.AsSpan();
		line = line.Trim();
		if( !markerRegex().IsMatch( line ) )
			return BlockState.None;

		string id = new string( line.Slice( 1 ) );
		PlaceholderBlock block = new( this, id );
		processor.NewBlocks.Push( block );
		return BlockState.BreakDiscard;
	}
}