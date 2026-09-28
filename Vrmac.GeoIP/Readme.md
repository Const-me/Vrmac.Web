# Vrmac.GeoIP

This project builds a .NET 10 DLL to efficiently resolve IP addresses into broad geographic regions.
See `public enum eRegion` for the definition of these regions.

In a synthetic benchmark on the AMD Ryzen 7 8700G processor,
I have measured 21 nanoseconds per IPv4 lookup.
IPv6 lookups are likely slower, but benchmarking is difficult due to the sparsity of the mapped address space.

You are unlikely to observe these numbers on a production server with many other components competing for CPU caches.
Still, for a single IPv4 lookup the theoretical worst-case latency is 9 cache misses.
That’s much slower than 21 nanoseconds but not terribly bad either.

The source dataset comes from MaxMind GeoLite Country.
However, the mmdb database format is quite inefficient.
This library is only fast because I have converted the database into custom optimised data format.
At the time of writing, the entire database, both IPv4 and IPv6 combined,
takes 678 kb on disk, and 4.76 mb in memory.

The current version of the library requires an AMD64 processor which supports BMI1 instruction set.
In the context of AOT compiled .NET 10, this means the `IlcInstructionSet` compiler option should be set to `avx2` or newer.

This DLL **does not** include the data conversion tool.\
See Vrmac.MaxMind project for the tool.

Note MaxMind GeoLite requires attribution and has license conditions.\
If you will use this library as opposed to merely reading this readme or compiling the codes,
make sure to check these terms and conditions.

The quality of the database is generally good, but not perfect.
To give a concrete example, MaxMind place the `168.168.192.0/21` block in Asia,
most other GeoIP providers (also the whois database) are geolocating the block in New York City US,
and some other GeoIP providers place it in Sydney Australia. Go figure.

## Compatibility

Both `Vrmac.MaxMind` tool and this `Vrmac.GeoIP` library require an AMD64 processor which runs a 64-bit OS
and supports [BMI1](https://en.wikipedia.org/wiki/X86_Bit_manipulation_instruction_set#BMI1_(Bit_Manipulation_Instruction_Set_1)) ISA extension.

For the .NET 10 AOT compiler, Microsoft has merged BMI1 feature flag into `avx2` value of the `IlcInstructionSet` compiler option.

## Source Data

The source database for use by this library is fetched and converted by another program.\
See `Vrmac.MaxMind` in the repository for the sources of that program.

Ideally, that tool should be AOT compiled.
Which is why unlike this library, the tool is not published on nuget.

To obtain the tool, please clone the git repository and build it yourself.\
The repository contains publish profiles to AOT compile that tool for Windows, conventional glibc-based Linux, and Alpine Linux operating systems.

Note Linux binary compatibility story is less than ideal.\
I recommend building that tool on the same, or at least very similar Linux where you’ll need to run the tool for real.

In runtime, that tool needs an API key, which in turn requires [a maxmind account](https://support.maxmind.com/knowledge-base/articles/create-a-maxmind-account).