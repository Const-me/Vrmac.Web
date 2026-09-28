namespace Vrmac;

/// <summary>Abstract class for elements parsed from the template</summary>
abstract class Element { }

/// <summary>A slice of static text; indentations are alreacy included.</summary>
sealed class Text: Element
{
	public readonly string text;
	public Text( string text ) => this.text = text;
	public override string ToString() => text;
}

/// <summary>A placeholder to inject content when applying the template</summary>
sealed class Placeholder: Element
{
	public readonly string name;
	public Placeholder( string name ) => this.name = name;
	public override string ToString() => "@" + name;
}

/// <summary>Conditional block which depends on a boolean flag supplied when applying the template</summary>
/// <remarks>The code generator converts conditional blocks into <c>if</c> branches.<br/>
/// This element is the sole reason why the DOM is a tree instead of merely a flat list.</remarks>
sealed class Block: Element
{
	public readonly string name;
	public readonly Element[] children;
	public Block( string name, IEnumerable<Element> children )
	{
		this.name = name;
		this.children = children.ToArray();
	}
	public override string ToString() => "Block: " + name;
}