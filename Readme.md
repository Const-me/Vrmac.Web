# Vrmac.Web

This repository contains a collection of .NET 10 libraries and tools I made
while developing the web site for my company, [Vrmac Softver](https://vrmac-softver.com/).

## Overview

* `Vrmac.Acme` – a client for [ACME v2](https://en.wikipedia.org/wiki/Automatic_Certificate_Management_Environment#API_version_2)
protocol to automatically obtain and renew [TLS](https://en.wikipedia.org/wiki/Transport_Layer_Security) certificates
from [let’s encrypt](https://letsencrypt.org/).
* `Vrmac.Admin` – building blocks to make interactive [SSH](https://en.wikipedia.org/wiki/Secure_Shell)-based remote Linux administration tools.
* `Vrmac.GeoIP` – a library to resolve IP addresses into broad geographic regions.
* `Vrmac.MaxMind` – a console program to update and convert MaxMind [GeoLite](https://www.maxmind.com/en/geolite-free-ip-geolocation-data) Country
database into the binary format expected by the `Vrmac.GeoIP` library.
Can be refactored into a class library with one line change in the csproj, see `Readme.md` in that subfolder for more information.
* `Vrmac.HtmlCompiler` – a [source generator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.iincrementalgenerator?view=roslyn-dotnet-5.0.0)
to compile HTML templates to strongly-typed C#.
* `Vrmac.Html` – a library implementing runtime support for web pages built from templates compiled by `Vrmac.HtmlCompiler` source generator.
* `Vrmac.Mapper` – micro-[ORM](https://en.wikipedia.org/wiki/Object%E2%80%93relational_mapping)
for [MariaDB](https://en.wikipedia.org/wiki/MariaDB) heavily inspired by [Dapper](https://github.com/DapperLib/Dapper).
* `Vrmac.Markdown` – a library to render [markdown](https://en.wikipedia.org/wiki/Markdown) to HTML.
* `Vrmac.Pwned` – a library to test passwords
against the “[Have I Been Pwned?](https://haveibeenpwned.com/)” dataset maintained by Troy Hunt.
* `Vrmac.PwnedFilter` – a design-time console program to build [Bloom filters](https://en.wikipedia.org/wiki/Bloom_filter) expected by the `Vrmac.Pwned` library.
* `Vrmac.RateLimits` – a library implementing [rate limiters](https://en.wikipedia.org/wiki/Rate_limiting).
* `Vrmac.Security` – a library implementing some higher-level cryptographic functions on top of the .NET 10 standard library.
* `Vrmac.Uploads` – a library implementing anonymous resumable content-addressable upload of large binary files.
* `Vrmac.Utils` – a few utility classes shared by other projects in this solution.

All these projects are written in idiomatic .NET 10 C# without unmanaged interop calls.

Most of them, except `Vrmac.Admin`, are compatible with [NativeAOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/?tabs=linux-alpine%2Cnet9plus)
with full trimming mode.

Most of them can be consumed from any .NET 10 application.
`Vrmac.Html` and `Vrmac.Uploads` depend on [ASP.NET Core](https://dotnet.microsoft.com/en-us/apps/aspnet) infrastructure
i.e. can only be used from Kestrel-based web servers.

Most of them are CPU-architecture agnostic;
`Vrmac.GeoIP` and `Vrmac.MaxMind` require an [AMD64](https://en.wikipedia.org/wiki/X86-64) processor with [AVX2](https://en.wikipedia.org/wiki/Advanced_Vector_Extensions#CPUs_with_AVX2) support.

I have only tested that code on Windows and Alpine Linux.

Please read `Security.md` document in this repository for some security-related tips.

## Rationale

I generally like the .NET ecosystem.\
C# is memory safe by default, very expressive, supports low-level stuff like platform intrinsics when necessary,
pretty good standard library with batteries included.
And NativeAOT solved both deployment and warmup latency very efficiently.

When I needed to develop a web server, using the .NET 10 / Kestrel stack was a no-brainer.\
Then I learned two things:
1. The AOT trimmer breaks many third-party libraries,
and even some parts of the standard library like [XmlSerializer](https://learn.microsoft.com/en-us/dotnet/api/system.xml.serialization.xmlserializer?view=net-10.0).
2. The quality of the lower-level half of the asp.net framework is great,
but the same can’t be said about the higher-level pieces.
Blazor pages allocate many small memory blocks for each request,
waste CPU time and memory bandwidth converting text between UTF-16 and UTF-8 representations,
are incompatible with NativeAOT, and break HTML indentation.

For these reasons, I decided to implement the missing pieces myself.

To be clear, the projects in this repository don’t contain any rocket science or trade secrets.
Quite the opposite: all of this code is admittedly boring boilerplate.

Which is why I’m publishing this repository under the permissive MIT licence, and calling it a day.