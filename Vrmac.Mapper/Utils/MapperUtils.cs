using MySqlConnector;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

/// <summary>Miscellaneous utility functions</summary>
public static class MapperUtils
{
	/// <summary>Make a simple pluralized string with the count, e.g. <c>"2 cats"</c></summary>
	/// <remarks>Handles basic English pluralization; does not cover irregular forms like "child / children" or "mouse / mice".</remarks>
	public static string pluralString( this int count, string single )
	{
		if( 1 != count )
		{
			if( single[ single.Length - 1 ] != 's' )
				return $"{count} {single}s";
			return $"{count} {single}es";
		}
		return $"1 {single}";
	}

	/// <summary>Read nullable timestamp column from the data reader</summary>
	public static DateTime? dateTimeOrNull( this MySqlDataReader reader, int ordinal )
	{
		if( reader.IsDBNull( ordinal ) )
			return null;
		return reader.GetDateTime( ordinal );
	}

	/// <summary>Serialize IP address into an array of 4 or 16 bytes</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public static byte[] toArray( this IPAddress ip )
	{
		Span<byte> span = stackalloc byte[ 16 ];
		if( !ip.TryWriteBytes( span, out int cb ) )
			throw new ArgumentException();

		if( cb == 16 )
		{
			Vector128<ushort> vec = Vector128.LoadUnsafe( ref MemoryMarshal.GetReference( span ) ).AsUInt16();
			Vector128<ushort> zero = Vector128<ushort>.Zero;
			Vector128<ushort> tmp = Sse2.Insert( zero, ushort.MaxValue, 5 );
			tmp = Sse2.Xor( vec, tmp );
			// Zero out the last 4 bytes in the vector
			tmp = Sse41.Blend( tmp, zero, 0b11000000 );
			// Now the vptest AVX1 instruction will return ZF flag when for the first 12 bytes vec == 0:0:0:0:0:FFFF
			if( Sse41.TestZ( tmp, tmp ) )
			{
				// Detected IPv4 address mapped to IPv6; that's how the Alpine+Kestrel stack delivers IPv4 addresses
				return span.Slice( 12, 4 ).ToArray();
			}

			if( vec.ToScalar() != 0x2002 )
			{
				// Actual IPv6 address
				return span.ToArray();
			}

			// Here's moar info on that crap: https://en.wikipedia.org/wiki/6to4
			return span.Slice( 2, 4 ).ToArray();
		}
		else if( cb == 4 )
			return span.Slice( 0, 4 ).ToArray();

		throw new ArgumentException();
	}
}