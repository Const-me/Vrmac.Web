# Vrmac.PwnedFilter

This project builds a command-line tool to produce and test Bloom filters
with the “[Have I Been Pwned?](https://haveibeenpwned.com/)” dataset.

Although the usage examples below show Windows commands,
the tool doesn’t use any OS-specific features and should work equally well on all platforms.

## Source Data

To download the source dataset, please use [haveibeenpwned-downloader](https://github.com/HaveIBeenPwned/PwnedPasswordsDownloader) tool.

Specifically, follow the “Download all SHA1 hashes to individual txt files into a custom directory called hashes”
usage example if you will then want incremental updates,
or “Download all SHA1 hashes to a single txt file called pwnedpasswords.txt”
if you don’t need incremental updates.

At the time of writing, windows usage examples on that page are slightly out of date.
It seems some .NET 10 SDK update changed the way [dotnet tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) are installed.
The fix is replacing `haveibeenpwned-downloader.exe` with `haveibeenpwned-downloader.cmd` in these commands.

Note these commands will download and save a large volume of data.\
Last time I checked, my `C:\Data\Pwned\hashes` folder exceeded 75 GB.

## Usage

### Dataset Summary

Print summary of the input dataset downloaded into a directory:

```
PwnedFilter.exe summary C:\Data\Pwned\hashes
```

Print summary of the input dataset downloaded into a single file:

```
PwnedFilter.exe summary C:\Data\Pwned\pwned.txt
```

### Build the Bloom Filter

Compile the dataset from the directory into a Bloom filter:

```
PwnedFilter.exe build C:\Data\Pwned\hashes C:\Data\Pwned\pwned.bin
```

Compile the dataset from the single file into a Bloom filter:

```
PwnedFilter.exe build C:\Data\Pwned\pwned.txt C:\Data\Pwned\pwned.bin
```

The tool will consume slightly over 4 GB RAM to complete that command.\
Due to the size of the dataset, the tool will use all CPU cores in your computer for a minute or two.

### Test the Bloom Filter

Type or paste passwords to try.\
When the password was on the list, or is a false positive, the tool will print `Pwned!`\
When the password is good, the tool will print `OK`

```
PwnedFilter.exe test C:\Data\Pwned\pwned.bin
```

## Windows “Security”

If you are using Windows,
and downloaded the source dataset as a folder with individual text files for incremental updates, 
the `summary` and `build` may take very long to complete.

To fix, open OS settings, “Virus & threat protection”, “Manage settings” link in the “Virus & threat protection settings” section,
“Add or remove exclusion” link. Then add your `hashes` directory to the list.

At least on my computer, loading 1 million small text files causes the `MsMpEng.exe` process to go haywire,
consuming like 95% of CPU time across all cores for no good reason.