# Vrmac.Markdown

This library contains functions to render markdown into HTML.

Specifically, it implements two alternative methods of doing so.

## MarkdownRender

`MarkdownRender` static class implements a fast but limited markdown renderer.

That class only supports the following features:

* Paragraphs and 3 levels of headers.
* Bold, italic, inline code blocks.
* Hyperlinks.

Not even lists are supported.

## MarkdownStandard

`MarkdownStandard` static class is based on [Markdig.Signed](https://www.nuget.org/packages/Markdig.Signed)
third-party library, and implements a fully featured, standard-compliant markdown renderer.
It uses more resources to render, particularly in terms of memory allocations.

### Placeholders extension

This renderer implements a custom extension.

To enable the extension, implement the `iMarkdownPlaceholders` interface
defined by this library in the consuming project:
```cs
/// <summary>Callback interface to inject custom blocks into the markdown</summary>
public interface iMarkdownPlaceholders
{
	/// <summary>Write content of the custom block</summary>
	void write( StreamWriter w, string id );
}
```
And pass your object to the `iMarkdownPlaceholders? placeholders` argument of the `render` function.

When the extension is enabled, the markdown parser tests lines
in the source text against the following regular expression:

```regexp
^\s*@([a-zA-Z][a-zA-Z0-9]*)\s*$
```

When a line matches, the renderer extracts the captured group.
Instead of interpreting the line as markdown, it calls the `iMarkdownPlaceholders.write` callback method,
passing the captured group’s value.