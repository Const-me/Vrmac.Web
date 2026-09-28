using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
namespace Vrmac.RateLimits;

/// <summary>Rate limiter which throttles requests per IP (IPv4) or subnet (IPv6)</summary>
public sealed class AnonRateLimiter
{
	readonly ulong subnetMask;
	readonly RateLimiter rateLimiter;

	/// <summary>Create the rate limiter</summary>
	public AnonRateLimiter( CancellationToken appStopping, byte subnet, int msRateLimit, TimeSpan cleanupFrequency )
	{
		ArgumentOutOfRangeException.ThrowIfLessThan( subnet, 32 );
		ArgumentOutOfRangeException.ThrowIfGreaterThan( subnet, 64 );
		subnetMask = ulong.MaxValue << ( 64 - subnet );

		rateLimiter = new( appStopping, msRateLimit, cleanupFrequency );
	}

	/// <summary>Create the rate limiter with 1 Hz limit which uses <c>/56</c> subnet for IPv6 addresses</summary>
	public AnonRateLimiter( CancellationToken appStopping ) :
		this( appStopping, 56, 1000, TimeSpan.FromSeconds( 256 ) )
	{ }

	/// <summary>If the request from that IP should complete ASAP, return a completed task.<br/>
	/// Otherwise, return the delay task to wait</summary>
	public ValueTask rateLimit( IPAddress ip ) =>
		RateLimitUtils.rateLimit( delay( ip ) );

	/// <summary>If the request from that IP should complete ASAP, return a completed task.<br/>
	/// Otherwise, return the delay task to wait</summary>
	public ValueTask rateLimit( IPAddress ip, CancellationToken cancel ) =>
		RateLimitUtils.rateLimit( delay( ip ), cancel );

	/// <summary>If the request from that IP should complete ASAP, return zero.<br/>
	/// Otherwise, return the delay which would have been awaited if the <see cref="rateLimit(IPAddress)" /> would have been called instead</summary>
	/// <remarks>Unlike <c>rateLimit</c> this method doesn't increase the delay, it's readonly</remarks>
	public TimeSpan currentLimit( IPAddress ip ) =>
		rateLimiter.currentLimit( makeKey( ip ) );

	/// <summary>Convert IP address into uint64 key for the hash map in the <see cref="RateLimiter"/> class</summary>
	[MethodImpl( MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining )]
	ulong makeKey( IPAddress address )
	{
		Span<byte> span = stackalloc byte[ 16 ];
		if( !address.TryWriteBytes( span, out int cbAddress ) )
			throw new ArgumentException();

		span = span.Slice( 0, cbAddress );
		if( span.Length == 16 )
		{
			if( isMappedIp4( span ) )
			{
				// IPv4-mapped IPv6 address
				span = span.Slice( 12 );
				goto Lookup4;
			}
			if( isTunelledIp4( span ) )
			{
				// https://en.wikipedia.org/wiki/6to4
				span = span.Slice( 2, 4 );
				goto Lookup4;
			}
			if( isTeredoIp4( span ) )
			{
				// https://en.wikipedia.org/wiki/Teredo_tunneling#IPv6_addressing
				span = span.Slice( 4, 4 );
				goto Lookup4;
			}
			// Actual IPv6 address
			return BinaryPrimitives.ReadUInt64BigEndian( span ) & subnetMask;
		}
		else if( span.Length == 4 )
			goto Lookup4;
		else
			throw new ArgumentException();

	Lookup4:
		Debug.Assert( span.Length == 4 );
		return 0xFFFFFFFF00000000ul | BinaryPrimitives.ReadUInt32BigEndian( span );
	}

	/// <summary><c>true</c> for IPv6 addresses from the "IPv4-IPv6 Translation Address" block <c>::ffff:0:0/96</c></summary>
	/// <remarks>This function is on the hot path because Alpine/Kestrel stack delivers all IPv4 addresses mapped into IPv6 space</remarks>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static bool isMappedIp4( ReadOnlySpan<byte> span )
	{
		// First 8 bytes must be zero, next 4 bytes must equal to [ 0, 0, 0xFF, 0xFF ]
		// Using bit tricks to optimize into a single branch
		uint b = BinaryPrimitives.ReadUInt32LittleEndian( span.Slice( 8 ) );
		ulong a = BinaryPrimitives.ReadUInt64LittleEndian( span );
		b ^= 0xFFFF0000u;
		a |= b;
		return a == 0;
	}

	/// <summary><c>true</c> for IPv6 addresses mapped from IPv4 using the crazy RFC 3056 BS</summary>
	/// <seealso href="https://en.wikipedia.org/wiki/6to4"/>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static bool isTunelledIp4( ReadOnlySpan<byte> span ) =>
		// Comparing first 2 bytes for equality with big endian 0x2002
		BinaryPrimitives.ReadUInt16LittleEndian( span ) == 0x0220;

	/// <summary><c>true</c> for IPv6 addresses mapped from IPv4 using the crazy (also deprecated) RFC 4380</summary>
	/// <seealso href="https://en.wikipedia.org/wiki/Teredo_tunneling"/>
	[MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
	static bool isTeredoIp4( ReadOnlySpan<byte> span ) =>
		// Comparing first 4 bytes for equality with big endian 0x20010000
		BinaryPrimitives.ReadUInt32LittleEndian( span ) == 0x0120;

	TimeSpan delay( IPAddress ip ) =>
		rateLimiter.rateLimit( makeKey( ip ) );
}