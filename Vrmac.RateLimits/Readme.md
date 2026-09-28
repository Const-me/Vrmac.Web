# Vrmac.RateLimits

This project builds a .NET 10 library to implement rate limits for network requests.\
Specifically, the library implements two public classes.

1. `AnonRateLimiter` implements a rate limiter which throttles requests per IP (IPv4) or subnet (IPv6).

2. `UserRateLimiter` implements a rate limiter which throttles requests based on uint64 keys.

The implementation is quite similar. The only difference is the key used in the hash maps.

The `rateLimit` public methods are thread safe and reentrant.

Note both classes should be [singletons](https://en.wikipedia.org/wiki/Singleton_pattern).\
They have internal mutable state to track time, and you only need a single instance of each.

The constructor of both classes takes a cancellation token.\
If you are consuming this library from an asp.net web app, I recommend passing the following token:
```C#
static AnonRateLimiter? anonRateLimiter = null;
static UserRateLimiter? userRateLimiter = null;

static void applicationStartup( WebApplication app )
{
	IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
	CancellationToken appStopping = lifetime.ApplicationStopping;

	// Construct both rate limiters: anonymous 1Hz per IP4 address or IP6 subnet, and 4Hz for users
	anonRateLimiter = new( appStopping );
	userRateLimiter = new( appStopping );
}
```