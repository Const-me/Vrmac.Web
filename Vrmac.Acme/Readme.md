# Vrmac.Acme

This library implements a .NET 10 client for
[ACME v2](https://en.wikipedia.org/wiki/Automatic_Certificate_Management_Environment#API_version_2) protocol.

The library is compatible with [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/?tabs=windows%2Cnet9plus),
tested with `TrimMode=Full` trimming option.

## Technical details

The public API is based on async-await.
The implementation relies on the thread pool implemented by the .NET runtime.

The main entry point is `ACME` static class.
Perhaps the most important member of that class is `create` factory function.
The function fetches and parses [directory](https://www.rfc-editor.org/info/rfc8555/#section-7.1.1) with endpoints,
and returns an object which acts as a callable proxy for the JSON RPC APIs implemented by the server.

All cryptography stuff is from the .NET 10 standard library.
All JSON stuff is implemented using a [source generator](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
from the standard library.

## Rationale

I have developed this library instead of using some pre-existing stuff for the following reasons.

* Standalone ACME clients write certificates to disk and expect the web server to notice
and reload them, via a restart or a reload hook.
I wanted to update certificates without restarting the web server.

* My web server runs under a service account with just barely sufficient permissions.
For security reasons, I wanted ACME client to run under the same service account.

* When I searched for an ACME client library, the ones I was able to find
were based on [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) which breaks down after the .NET 10 AOT trimmer.
Also some of them bring their own crypto dependency,
like [Bouncy Castle](https://en.wikipedia.org/wiki/Bouncy_Castle_(cryptography)),
even though the .NET 10 standard library comes with all cryptography stuff necessary for the use case.

## Limitations

Only tested with the free let’s encrypt ACME server.

Only tested with Kestrel web server AOT compiled with .NET 10 SDK.
My particular server has an AMD64 CPU and runs Alpine Linux,
albeit none of that should matter as this library is written in idiomatic memory-safe C# with no native interop.