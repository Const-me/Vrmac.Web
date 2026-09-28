using System.Runtime.CompilerServices;
using Vrmac;
namespace Markdown;

/// <summary>Accumulates parsed and binary serialized markdown</summary>
struct MarkdownBuilder
{
	public byte[] toArray()
	{
		trimLastNewline();
		return list.ToArray();
	}

	public MarkdownBuilder( bool keepFirstH1 = false )
	{
		list = new();
		this.keepFirstH1 = keepFirstH1;
	}

	readonly List<byte> list;
	readonly bool keepFirstH1;
	public byte[]? firstH1 { get; private set; }

	/// <summary>Add a slice of text</summary>
	public void text( ReadOnlySpan<byte> span )
	{
		int cb = span.Length;
		if( cb < 0x7F )
			list.Add( (byte)cb );
		else
		{
			list.Add( 0x7F );
			MultiByte.encode( list, cb - 0x7F );
		}
		list.AddRange( span );
		lastElement = default;
	}

	public void headerText( eBinaryElement elt, ReadOnlySpan<byte> span )
	{
		if( !keepFirstH1 || elt != eBinaryElement.Heading1 || span.IsEmpty || null != firstH1 )
			return;
		firstH1 = span.ToArray();
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void element( eBinaryElement e )
	{
		list.Add( (byte)e );
		lastElement = e;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public void closeElement( eBinaryElement e )
	{
		byte b = (byte)e;
		b |= (byte)eBinaryElement.ClosingTag;
		list.Add( b );
		lastElement = (eBinaryElement)b;
	}

	public eBinaryElement lastElement { get; private set; }

	public void trimLastNewline()
	{
		switch( lastElement )
		{
			case eBinaryElement.NewLine:
			case eBinaryElement.LineBreak:
				break;
			default:
				return;
		}
		list.RemoveAt( list.Count - 1 );
		lastElement = default;
	}
}