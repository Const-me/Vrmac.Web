using System.Buffers.Binary;
using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Vrmac;

/// <summary>Utility functions for compact string encoding of unsigned integers</summary>
public static class CompressedID
{
	/// <summary>Write a number encoded into Base64Url</summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	public static void format( this Stream stream, ulong number )
	{
		Span<byte> bytes = stackalloc byte[ 8 ];
		BinaryPrimitives.WriteUInt64LittleEndian( bytes, number );

		// Compute count of leading zero bytes in the number: two fast instructions, LZCNT and right shift
		int zeroBytes = BitOperations.LeadingZeroCount( number ) / 8;
		// Clamp to generate something non-empty even for zero input
		zeroBytes = Math.Min( zeroBytes, 7 );
		// Truncate zero bytes, note these are trailing now because processors are little endian
		bytes = bytes.Slice( 0, 8 - zeroBytes );

		Span<byte> chars = stackalloc byte[ Base64Url.GetEncodedLength( bytes.Length ) ];
		if( !Base64Url.TryEncodeToUtf8( bytes, chars, out _ ) )
			throw new FormatException();
		stream.Write( chars );
	}

	/// <summary>Parse a number encoded into Base64Url</summary>
	public static bool parse( ReadOnlySpan<char> chars, out ulong number )
	{
		Span<byte> bytes = stackalloc byte[ 8 ];
		// The following line should compile into 1 fast instruction, not so sure about Clear() method
		MemoryMarshal.Write( bytes, 0UL );

		if( !Base64Url.TryDecodeFromChars( chars, bytes, out int cb ) )
		{
			number = 0;
			return false;
		}

		number = BinaryPrimitives.ReadUInt64LittleEndian( bytes );
		return true;
	}
}