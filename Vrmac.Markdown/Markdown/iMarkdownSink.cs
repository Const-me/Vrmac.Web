namespace Markdown;

enum eTextSpan: byte
{
	Bold, Italic, Consolas
}

enum eParagraph: byte
{
	Normal, H1, H2, H3
}

interface iMarkdownSink
{
	void text( ReadOnlySpan<byte> span );
	void newLine();
	void lineBreak();
	void span( eTextSpan style, bool close );
	void para( eParagraph style, bool close );
	void linkBegin( ReadOnlySpan<byte> url );
	void linkEnd();
}