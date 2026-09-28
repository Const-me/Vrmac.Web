using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
namespace Vrmac.Html;

/// <summary>Extension methods to write UTF-8 text into the stream</summary>
public static class Utf8StreamExtensions
{
	/// <summary>Write pre-rendered HTML content</summary>
	public static void write( this Stream stream, ReadOnlySpan<byte> bytes ) =>
		stream.Write( bytes );

	/// <summary>Write pre-rendered HTML content</summary>
	public static void write( this Stream stream, ReadOnlyMemory<byte> bytes ) =>
		stream.Write( bytes.Span );

	/// <summary>Write a single '\n' byte to the stream</summary>
	public static void newLine( this Stream stream ) =>
		stream.WriteByte( (byte)'\n' );

	/// <summary>Write formatted decimal number</summary>
	public static void number( this Stream stream, ulong number )
	{
		Span<byte> span = stackalloc byte[ 24 ];
		if( !number.TryFormat( span, out int cb ) )
			throw new FormatException();
		stream.Write( span.Slice( 0, cb ) );
	}

	/// <summary>Print a timestamp</summary>
	public static void writeTimestamp( this Stream stream, DateTime timestamp )
	{
		TimeSpan ts = DateTime.UtcNow - timestamp;
		if( ts.Ticks <= 0 )
		{
			stream.Write( "in the future"u8 );
			return;
		}
		if( ts.Ticks < TimeSpan.TicksPerMinute )
		{
			seconds( stream, ts );
			return;
		}
		if( ts.Ticks < TimeSpan.TicksPerHour )
		{
			formatted( stream, ts, @"mm\m\ ss" );
			stream.Write( "s ago"u8 );
			return;
		}
		if( ts.Ticks < TimeSpan.TicksPerDay )
		{
			formatted( stream, ts, @"hh\h\ mm" );
			stream.Write( "m ago"u8 );
			return;
		}
		if( ts.Ticks < TimeSpan.TicksPerDay * 8 )
		{
			// Divide rounding to nearest
			int daysCount = (int)( ( ts.Ticks + ( TimeSpan.TicksPerDay / 2 - 1 ) ) / TimeSpan.TicksPerDay );
			days( stream, daysCount );
			return;
		}
		stream.isoDate( timestamp );
	}

	[MethodImpl( MethodImplOptions.NoInlining )]
	static void seconds( Stream stream, TimeSpan ts )
	{
		double sec = ts.TotalSeconds;
		Span<byte> span = stackalloc byte[ 8 ];
		int cb;
		if( !sec.TryFormat( span, out cb, "F1", CultureInfo.InvariantCulture ) )
			throw new ApplicationException();
		stream.Write( span.Slice( 0, cb ) );
		stream.Write( " seconds ago"u8 );
	}

	[MethodImpl( MethodImplOptions.NoInlining )]
	static void formatted( Stream stream, TimeSpan ts, string fmt )
	{
		Span<byte> span = stackalloc byte[ 16 ];
		int cb;
		if( !ts.TryFormat( span, out cb, fmt, CultureInfo.InvariantCulture ) )
			throw new ApplicationException();
		stream.Write( span.Slice( 0, cb ) );
	}

	/// <summary>Write ISO 8601 date string</summary>
	[MethodImpl( MethodImplOptions.NoInlining )]
	public static void isoDate( this Stream stream, DateTime timestamp )
	{
		Span<byte> span = stackalloc byte[ 16 ];
		int cb;
		if( !timestamp.TryFormat( span, out cb, "yyyy-MM-dd", CultureInfo.InvariantCulture ) )
			throw new FormatException();
		stream.Write( span.Slice( 0, cb ) );
	}

	[MethodImpl( MethodImplOptions.NoInlining )]
	static void days( Stream stream, int count )
	{
		if( 1 == count )
		{
			stream.write( "1 day ago"u8 );
			return;
		}

		Span<byte> span = stackalloc byte[ 8 ];
		int cb;
		if( !count.TryFormat( span, out cb ) )
			throw new FormatException();
		stream.Write( span.Slice( 0, cb ) );
		stream.Write( " days ago"u8 );
	}

	/// <summary>Write UTF-16 string</summary>
	[MethodImpl( MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization )]
	public static void writeRawText( this Stream stream, ReadOnlySpan<char> str )
	{
		Encoding encoding = Encoding.UTF8;
		int cb = encoding.GetByteCount( str );

		const int maxStackBytes = 1024 * 2;

		if( cb <= maxStackBytes )
		{
			Span<byte> span = stackalloc byte[ cb ];
			encoding.GetBytes( str, span );
			stream.Write( span );
		}
		else
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Shared;
			byte[] arr = pool.Rent( cb );
			try
			{
				Span<byte> span = arr.AsSpan( 0, cb );
				encoding.GetBytes( str, span );
				stream.Write( span );
			}
			finally
			{
				pool.Return( arr );
			}
		}
	}
}