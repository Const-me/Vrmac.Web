using System.Buffers.Binary;
using System.Runtime.CompilerServices;
namespace Vrmac;

/// <summary>Utility functions implementing RFC 8794 variable-length integers</summary>
public static class MultiByte
{
	/// <summary>Append encoded variable-length <c>uint</c> to the list of bytes</summary>
	[MethodImpl( MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining )]
	public static void encode( List<byte> list, int val )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( val );

		unchecked
		{
			if( val < 0x80 )
			{
				list.Add( (byte)( val | 0x80 ) );
				return;
			}

			Span<byte> span = stackalloc byte[ 5 ];
			BinaryPrimitives.WriteInt32BigEndian( span.Slice( 1 ), val );
			if( val < 0x4000 )
			{
				span = span.Slice( 3 );
				span[ 0 ] |= 0x40;
			}
			else if( val < 0x200000 )
			{
				span = span.Slice( 2 );
				span[ 0 ] |= 0x20;
			}
			else if( val < 0x10000000 )
			{
				span = span.Slice( 1 );
				span[ 0 ] |= 0x10;
			}
			else
				span[ 0 ] = 0x08;

			list.AddRange( span );
		}
	}

	/// <summary>Parse 4-bytes unsigned integer from a buffer</summary>
	[MethodImpl( MethodImplOptions.AggressiveOptimization )]
	public static int parse( ReadOnlySpan<byte> span, out int cbConsumed )
	{
		unchecked
		{
			byte header = span[ 0 ];
			if( 0 != ( header & 0x80 ) )
			{
				// 1 bit header, 7 bits payload
				header &= 0x7F;
				cbConsumed = 1;
				return header;
			}
			if( 0 != ( header & 0x40 ) )
			{
				// 2 bits header, 14 bits payload
				int res = BinaryPrimitives.ReadUInt16BigEndian( span.Slice( 0, 2 ) );
				cbConsumed = 2;
				res &= 0x3FFF;
				return res;
			}

			if( 0 != ( header & 0x20 ) )
			{
				// 3 bits header, 21 bits payload
				int low = BinaryPrimitives.ReadUInt16BigEndian( span.Slice( 1, 2 ) );
				cbConsumed = 3;
				int high = header & 0x1F;
				high <<= 16;
				return high | low;
			}
			if( 0 != ( header & 0x10 ) )
			{
				// 4 bits header, 28 bits payload
				int res = BinaryPrimitives.ReadInt32BigEndian( span.Slice( 0, 4 ) );
				cbConsumed = 4;
				res &= 0x0FFFFFFF;
				return res;
			}
			if( header == 8 )
			{
				// Checking for exact equality because we want > 32 bits to result in ArgumentOutOfRangeException()
				// That RFC 8794 supports uint64 encoded into 9 bytes, we just don't support such a large numbers in this library
				int res = BinaryPrimitives.ReadInt32BigEndian( span.Slice( 1, 4 ) );
				ArgumentOutOfRangeException.ThrowIfNegative( res );
				cbConsumed = 5;
				return res;
			}
			throw new ArgumentOutOfRangeException();
		}
	}
}