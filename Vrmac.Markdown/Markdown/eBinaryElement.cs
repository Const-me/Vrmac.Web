namespace Markdown;

enum eBinaryElement: byte
{
	// 0 .. 0x7F range reserved for text spans

	/// <summary>New line in source markdown</summary>
	NewLine = 0x80,
	/// <summary>Line break followed by the new line in source markdown</summary>
	LineBreak = 0x81,
	/// <summary>Start of the <c>**bold**</c> span, or end with <see cref="ClosingTag" /> bit</summary>
	SpanBold = 0x82,
	/// <summary>Start of the <c>*italic*</c> span, or end with <see cref="ClosingTag" /> bit</summary>
	SpanItalic = 0x83,
	/// <summary>Start of the <c>`consolas`</c> span, or end with <see cref="ClosingTag" /> bit</summary>
	SpanConsolas = 0x84,
	/// <summary>The next element is text with hyperlink URL, then text or spans, then Hyperlink with <see cref="ClosingTag" /> bit</summary>
	Hyperlink = 0x85,

	/// <summary>Start of the normal paragraph, or the end with <see cref="ClosingTag" /> bit</summary>
	Paragraph = 0x86,
	/// <summary>Start of the H1 paragraph, or the end with <see cref="ClosingTag" /> bit</summary>
	Heading1 = 0x87,
	/// <summary>Start of the H2 paragraph, or the end with <see cref="ClosingTag" /> bit</summary>
	Heading2 = 0x88,
	/// <summary>Start of the H2 paragraph, or the end with <see cref="ClosingTag" /> bit</summary>
	Heading3 = 0x89,

	/// <summary>Special bit to mark end of paragraph or spans</summary>
	ClosingTag = 0x40,
}