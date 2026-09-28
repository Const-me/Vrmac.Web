# Vrmac.Utils

This library implements a few utility classes consumed by other `Vrmac.*` packages.

* `Spans` static class contains functions to concatenate spans of bytes into newly allocated arrays.
* `MultiByte` static class implements variable-length integer codec specified in the section 4 “Variable-Size Integer” 
of [RFC 8794](https://www.rfc-editor.org/rfc/rfc8794.pdf). The class only supports integers in [ 0 .. 2^31 - 1 ] interval.
* `LineParser` static class implements a function to split ASCII or UTF-8 text in memory into lines without copying the data.
It supports Windows and Unix line ending conventions, also the mix of them.
* `RentedArray` structure is a [RAII](https://en.wikipedia.org/wiki/Resource_acquisition_is_initialization) wrapper 
around array of bytes rented from [the pool](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1?view=net-10.0),
along with HTTP status code. The array is optional; can also be constructed with just the status.
* `Utf8StreamExtensions` static class contains extension methods to write bytes, printed integers, and UTC timestamp strings into UTF-8 stream of bytes.
* `HtmlEncoder` static class implements functions similar to 
[WebUtility.HtmlEncode](https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode?view=net-10.0)
producing UTF-8 output instead of UTF-16.
* `CachedMemStream` static class implements a pool of cached reusable
[MemoryStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.memorystream?view=net-10.0) objects, at most one per thread.
Do not retain the streams obtained from `CachedMemStream.threadLocal()` function across `await`-s, 
things will probably fail catastrophically because two threads will end up sharing one stream.
* `CompressedID` static class implements functions to format and parse unsigned numbers for/from HTTP query string values.
Designed for primary keys like this:
```sql
oid bigint UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY COMMENT 'Primary key, int64'
```
Note these numbers, as well as their encoded representation, are easily predictable, and the class does nothing to address that.
When security matters, do something reasonable in the consuming code.