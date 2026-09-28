using System.Buffers.Binary;
using System.Runtime.CompilerServices;
namespace Vrmac;

/// <summary>Split text into lines</summary>
public static class LineParser
{
	internal static ReadOnlySpan<byte> byteOrderMark => [ 0xEF, 0xBB, 0xBF ];

	/// <summary>Split ASCII or UTF-8 text into lines, without making copies</summary>
	public static IEnumerable<ReadOnlyMemory<byte>> parse( ReadOnlyMemory<byte> mem )
	{
		if( mem.Length >= 3 && mem.Span.StartsWith( byteOrderMark ) )
			mem = mem.Slice( 3 );

		while( !mem.IsEmpty )
		{
			int idx = mem.Span.IndexOfAny( (byte)'\r', (byte)'\n' );
			if( idx < 0 )
			{
				yield return mem;
				break;
			}

			yield return mem.Slice( 0, idx );
			mem = mem.Slice( idx );
			mem = mem.Slice( newlineLength( mem ) );
		}
	}

	/// <summary>Length of the new line in bytes, either 1 or 2</summary>
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	static int newlineLength( ReadOnlyMemory<byte> mem )
	{
		if( mem.Length < 2 )
			return 1;
		const ushort crlf = '\r' | ( '\n' << 8 );
		bool extra = BinaryPrimitives.ReadUInt16LittleEndian( mem.Span ) == crlf;
		// Trying to convince the compiler to output `sete` instruction https://www.felixcloutier.com/x86/setcc
		return 1 + ( extra ? 1 : 0 );
	}
}