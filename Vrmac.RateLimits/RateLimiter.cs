using System.Runtime.InteropServices;
namespace Vrmac.RateLimits;

/// <summary>Implementation of a rate limiter</summary>
sealed class RateLimiter
{
	readonly int msRateLimit;
	readonly CancellationToken appStopping;
	readonly TimeSpan cleanupFrequency;

	public RateLimiter( CancellationToken appStopping, int msRateLimit, TimeSpan cleanupFrequency )
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( msRateLimit );
		ArgumentOutOfRangeException.ThrowIfLessThan( cleanupFrequency.Ticks, TimeSpan.TicksPerSecond );

		// The initial capacity for collection is very small number, probably 4.
		// Allocate slightly more memory on startup.
		const int initialCapacity = 64;
		dict = new( initialCapacity );
		expiredKeys = new( initialCapacity );

		this.msRateLimit = msRateLimit;
		this.appStopping = appStopping;
		this.cleanupFrequency = cleanupFrequency;
	}

	readonly object syncRoot = new();
	readonly Dictionary<ulong, long> dict;
	bool cleanupScheduled = false;
	readonly List<ulong> expiredKeys;

	/// <summary>If the request with that key should complete instantly, return zero.<br/>
	/// Otherwise, return the duration to sleep with <see cref="Task.Delay(TimeSpan)" /></summary>
	public TimeSpan rateLimit( ulong key )
	{
		long now = Environment.TickCount64;
		TimeSpan result;
		lock( syncRoot )
		{
			ref long entry = ref CollectionsMarshal.GetValueRefOrAddDefault( dict, key, out bool wasThere );
			if( !wasThere || entry <= now )
			{
				entry = now + msRateLimit;
				result = TimeSpan.Zero;
			}
			else
			{
				result = TimeSpan.FromTicks( ( entry - now ) * TimeSpan.TicksPerMillisecond );
				entry += msRateLimit;
			}

			// Unless already scheduled, launch the background cleanup job
			if( !cleanupScheduled )
			{
				cleanupScheduled = true;
				backgroundCleanupTask().forget();
			}
		}
		return result;
	}

	/// <summary>If the request with that key will complete instantly, return zero.<br/>
	/// Otherwise, return the duration the <see cref="rateLimit" /> would have returned</summary>
	/// <remarks>Unlike <see cref="rateLimit" /> this method doesn't increase the delay</remarks>
	public TimeSpan currentLimit( ulong key )
	{
		long entry;
		lock( syncRoot )
		{
			if( !dict.TryGetValue( key, out entry ) )
				return TimeSpan.Zero;
		}
		long wait = entry - Environment.TickCount64;
		wait = Math.Max( wait, 0 );
		return TimeSpan.FromTicks( wait * TimeSpan.TicksPerMillisecond );
	}

	/// <summary>Background maintenance job to remove old keys from the dictionary</summary>
	/// <remarks>Without this function, the class would leak memory under load</remarks>
	async Task backgroundCleanupTask()
	{
		while( true )
		{
			await Task.Delay( cleanupFrequency, appStopping );

			expiredKeys.Clear();
			long now = Environment.TickCount64;

			lock( syncRoot )
			{
				bool anyLeft = false;
				foreach( var kvp in dict )
				{
					if( kvp.Value <= now )
						expiredKeys.Add( kvp.Key );
					else
						anyLeft = true;
				}
				if( !anyLeft )
				{
					dict.Clear();
					cleanupScheduled = false;
					return;
				}
				foreach( ulong key in expiredKeys )
					dict.Remove( key );
			}
		}
	}
}