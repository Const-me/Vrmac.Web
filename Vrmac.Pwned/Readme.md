# Vrmac.Pwned

This project builds a .NET library to test passwords
against the “[Have I Been Pwned?](https://haveibeenpwned.com/)” dataset maintained by Troy Hunt.

To save storage and memory, the OG dataset is compressed
into a [Bloom filter](https://en.wikipedia.org/wiki/Bloom_filter).\
The source dataset contains 38.5 GB of binary hashes (text files are much larger, over 75 GB),
whilst the Bloom filter is a much more manageable 4 GB.

Note that Bloom filters are probabilistic: there is a non-zero probability of false positives.\
Specifically, with this implementation that probability is 0.033%, which is not terribly bad for most purposes.

## Usage

The only public class in this library is `PwnedQuery`.\
Construct it by passing the path to the Bloom filter file on disk.\
Network-mounted paths are not recommended and may not work reliably.

Once constructed, call `public bool query( string passwordText )` method, passing the password to test.\
The method is thread-safe and reentrant.

When `query` returns `false`, the password is definitely not in the breached dataset.\
When it returns `true`, the password is most likely in the breached dataset,
with a 0.033% chance of being a false positive.

## Filter File

You may be wondering where to obtain the 4 GB filter file.

See “Vrmac.PwnedFilter” project in this repository for the tool
that builds it from the original dataset.

Alternatively, if you would rather not download 75.6 GB of ASCII text files,
use `Pwned.2026-09-26.torrent` file in this repository to download a pre-built filter.
The torrent contains a single file `pwned.bin`.
Download that file, and pass the absolute path to the constructor of the `PwnedQuery` class.

Ideally, verify SHA-512 checksum of that file after the download. On Windows:

```bat
certutil -hashfile pwned.bin SHA512
SHA512 hash of pwned.bin:
91b0723c4324c1d599099e4ef47cd19169a65849ed591308c394d1799aec45ad31c1ad2b3796d753dccc50e2a6488c62ff3330c082db823aa6667c3722ac3aa1
CertUtil: -hashfile command completed successfully.
```

On Linux:

```bash
sha512sum pwned.bin
91b0723c4324c1d599099e4ef47cd19169a65849ed591308c394d1799aec45ad31c1ad2b3796d753dccc50e2a6488c62ff3330c082db823aa6667c3722ac3aa1  pwned.bin
```

I can’t promise I will update that binary file regularly, or at all.
If you want timely updates, please use the `Vrmac.PwnedFilter` tool to build your own version of the binary file.