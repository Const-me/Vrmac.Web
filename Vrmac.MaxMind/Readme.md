# Vrmac.MaxMind

This project builds a program to convert MaxMind GeoLite Country database
into the efficient binary format for use by the `Vrmac.GeoIP` library.

I do not have the rights to redistribute the dataset, you gonna need an API key to use this tool.

The tool is designed for integration into other software not for direct human use, so the usability is rather limited.

The tool takes a single command-line argument, absolute path to a cache directory.
And it requires an environment variable `MAXMIND_AUTH`
with the Base64-encoded `user:password` credentials, used as the value of the HTTP Basic Authorization header.

If the cache directory already has recent database, the tool will do pretty much nothing,
a single HEAD http request. If the cache folder is empty, or the maxmind has published an update,
the tool will download and convert the new database.
In either case, a `geoip.bin` file will be present in the cache directory when the tool exits.

The output is a single line in the stderr with 2 tab-separated fields: int32 status code, and string status message.
The codes are defined by [MS-ERREF 2.1 HRESULT](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/0642cb2f-2075-4469-918c-4441e69c548a) spec.

Status code `0` S_OK means the tool has downloaded and converted a new version of the database.\
Status code `1` S_FALSE means the local data is already recent.\
Negative numbers are error codes and the tool prints them in hexadecimal, for example `0x80070057`

If you are wondering why the tool is a stand-alone program instead of being a library, it’s due to the memory use.
Database conversion process consumes non-trivial amount of memory, on Windows I’ve observed slightly under 450 MB of RAM.
Most of that memory is used by millions of small [GC](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/) managed
objects. Once the DB is converted, it’s quite expensive to trace the objects and collect the memory.

That’s why the tool is a standalone program.
When the process quits, the OS reclaims the memory without spending CPU time collecting individual objects.

If you want a library instead, remove the `<OutputType>Exe</OutputType>` line from the `Vrmac.MaxMind.csproj`,
call the `MaxMindUpdate.updateIfNeeded` function to download, and `MaxMindUpdate.convertDatabase` to convert the database.

## Forward Compatibility

The MIT license’s no-warranty clause already covers this, but I’d like to elaborate.

Although it’s been working in my environment for 4 months and counting,
I can’t guarantee this program will continue to stay compatible with future GeoIP databases provided by MaxMind.

Here’s an incomplete list of possible reasons why the database converter might break in the future.

* This tool requires database version = 6, and I have no idea how specifically they are numbered.
* This tool requires 24 bits tree nodes.
* Metadata deserialiser in this tool doesn’t support arrays of objects.

The database download step is not fully reliable either.\
Just like any other third-party service, MaxMind has the technical ability to change HTTP endpoints,
supported authorisation, file naming conventions, compression, or other important technical details.
Such changes will probably break this tool. Also, no networks are 100% reliable.

Wait, there’s more.

When you registered on maxmind.com to get the API key to use this tool,
you accepted the [EULA](https://www.maxmind.com/en/end-user-license-agreement).
Among other things, that EULA says you must destroy old versions of the GeoIP DB
within 30 days after MaxMind releases an update.

This means if this tool breaks and you don’t react within 30 days, you will probably violate the EULA.

Make sure you parse and monitor standard error of the tool to react in time.\
At minimum, check the tool’s exit code: non-zero means the tool failed to complete successfully.

## Pre-Existing IP

### MaxMind DB Reader

This project contains parts copy-pasted and/or adapted from the [MaxMind DB Reader](https://github.com/maxmind/MaxMind-DB-Reader-dotnet) library.\
That library is copyright © 2013-2026 by MaxMind, Inc.\
I believe I have used version 5.0.0 of the library back then.

That free software is licensed under the Apache License Version 2.0.\
See `Apache-2.0.txt` in this folder for the licence text.

### GeoNames Database

The `Transform` subfolder contains a table derived from the [GeoNames](https://www.geonames.org/) geographical database,
specifically `regionFromCountry.txt` and `regionFromCountry.gz` files, with substantial modifications.\
The unmodified upstream file is also included in the same folder, as `countryInfo.txt`.

GeoNames data is licenced under a [Creative Commons Attribution 4.0 License](https://creativecommons.org/licenses/by/4.0/).