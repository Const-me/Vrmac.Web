namespace Vrmac.RateLimits;

/// <summary>Rate limiter which throttles requests based on uint64 keys</summary>
public sealed class UserRateLimiter
{
	readonly RateLimiter rateLimiter;

	/// <summary>Create the rate limiter</summary>
	public UserRateLimiter( CancellationToken appStopping, int msRateLimit, TimeSpan cleanupFrequency ) =>
		rateLimiter = new( appStopping, msRateLimit, cleanupFrequency );

	/// <summary>Create the rate limiter with 4 Hz limit</summary>
	public UserRateLimiter( CancellationToken appStopping ) :
		this( appStopping, 1000 / 4, TimeSpan.FromSeconds( 60 ) )
	{ }

	/// <summary>If the request with that key should complete instantly, return a completed task.<br/>
	/// Otherwise, return the delay task to wait</summary>
	public ValueTask rateLimit( ulong key ) =>
		RateLimitUtils.rateLimit( rateLimiter.rateLimit( key ) );

	/// <summary>If the request with that key should complete ASAP, return a completed task.<br/>
	/// Otherwise, return the delay task to wait</summary>
	public ValueTask rateLimit( ulong key, CancellationToken cancel ) =>
		RateLimitUtils.rateLimit( rateLimiter.rateLimit( key ), cancel );
}