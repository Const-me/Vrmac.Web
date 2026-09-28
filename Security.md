# Security Shenanigans

Securely operating a web server comes with unique challenges.
You need at least basic understanding of the entire software stack.

## Linux

### Service Account

You should never run any servers under the root account.\
Instead, create a special low privilege service account, ideally a sole member of the group with the same name.

To allow the server to listen on ports 80 (http) and 443 (https),
run the following command as root: `setcap 'cap_net_bind_service=+ep' /your/server/binary`
The metadata set by the `setcap` might be lost after the file is copied or renamed.
Ideally, run `setcap` with the path to your server ELF file which will be launched for the production use.

On Alpine Linux, the `setcap` binary is provided by the [libcap](https://pkgs.alpinelinux.org/package/edge/main/x86_64/libcap)
which is missing from the default OS installation.

Make sure the service account has minimum sufficient permissions.
In particular, Linux service accounts generally have no need for an interactive login.
The ideal shell for them is `/sbin/nologin` or an equivalent.

Many people on the internets recommend containers like [docker](https://en.wikipedia.org/wiki/Docker_(software)), but I think they are overrated.
With careful setup, user accounts provide a security boundary equally strong to containers.
The .NET 10 AOT compiler produces a single [ELF](https://en.wikipedia.org/wiki/Executable_and_Linkable_Format) file,
which makes atomic deployment and rollback straightforward.
Containers introduce unnecessary complexity, and therefore reliability risks in the long run when the OS and software are being updated, 
while delivering too little value.

### OpenRC Integration

OpenRC doesn’t have an equivalent of the systemd [credentials](https://systemd.io/CREDENTIALS/).
And the supervise-daemon won’t run the `start_pre()` function after your server quits or crashes.

To reliably run stuff under the root account on every restart of the server, I suggest the following workaround.

1. Install [su-exec](https://pkgs.alpinelinux.org/package/edge/main/x86_64/su-exec) package.
2. In your service script, do not set `command_user` so the server is launched under root account.\
Instead of directly launching your server, launch a special shell script, owned by `root:root` with `500` permissions.
3. In that shell script, do whatever you have to do to securely inject secrets, then launch your server like that:\
`exec su-exec svc:svc /your/server/binary` where `svc` is the service account for the server.

For reliable error handling, the first command in the launcher shell script should be `set -e` or `set -eu`

### Environment Variables

Unlike Windows or MacOS, Linux doesn’t have an equivalent of certificate store or the keychain.

Many examples on the internets suggest injecting secrets using environment variables.

The tactic is questionable because the OS makes an immutable snapshot of the environment variables at the time a process is launched.

The snapshot remains visible under `/proc` even if the process clears the environment variables early on startup.
It is visible to root, and to the user who runs the process.
The snapshot is only removed when the process quits.

To mitigate, do something better.
If you have systemd, consider the [credentials](https://systemd.io/CREDENTIALS/) feature.
With [OpenRC](https://en.wikipedia.org/wiki/OpenRC), you might need to implement something similar on your own.

### File Security

Owner of files or directories has full control over them.

The documentation for the OS APIs and utility programs
like [chmod](https://www.man7.org/linux/man-pages/man1/chmod.1.html) disagrees,
documenting 3 bits or command line switches which control owner read, owner write, and owner execute permissions.
Sadly, these bits protect nothing because by design, the owner can always change the file’s permission bits, regardless of what those bits currently say.

A good workaround for the files which need restricted access is setting owner=root,
and assign actual permissions to the group.
Members of the group don’t have the permission to write permissions, even for files where the group has full control.

### Directory Security

By default, operations like rename or delete ignore permissions on the file being renamed or deleted.
Instead, the OS only checks permissions on the directory containing the file.

A good workaround for the directories which need restricted access to the content is setting owner=root,
assign actual permissions to the group.\
And if that directory ever contains files owned by different accounts, even temporarily so,
make sure to set sticky bit for the directory to avoid surprises.

### Symbolic Links

Linux symbolic links have a potential to cause security bugs for the following reasons.

* [chown](https://www.man7.org/linux/man-pages/man1/chown.1.html) command line program follows them by default. To mitigate, add `-h` switch.
* When a process opens a file, by default the OS follows the links.
The [kernel API](https://www.man7.org/linux/man-pages/man2/open.2.html) has `O_NOFOLLOW` flag to mitigate.
Sadly, that flag is not exposed in the .NET standard library. A partial mitigation is testing for
`if( null != File.ResolveLinkTarget( path, false ) )`: not 100% reliable under concurrency, but it’s much better than nothing.

## Microsoft .NET

### File Permissions

On Linux, the standard library includes
[FileStreamOptions.UnixCreateMode](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestreamoptions.unixcreatemode?view=net-10.0) property.

However, when the FileMode is Create or OpenOrCreate and the file had been already there, the UnixCreateMode property is ignored.
To mitigate, call `File.SetUnixFileMode( fileStream.SafeFileHandle, ufm )` immediately after you got the FileStream object.

### Profile Directory

I don’t believe Microsoft has documented that, but apparently ASP.NET Core servers need write access
to the home directory of the user running the process.

Specifically, Kestrel web server creates `~/.dotnet` directory with a deep tree of children
to keep intermediate certificates for the chain of trust of the TLS certificate used by the server.

Make sure your service account has a writeable home directory.

### Diagnostic Backdoor

By default, .NET programs contain a backdoor implemented for debugging and diagnostic tools
like dotnet-trace, dotnet-counters, dotnet-dump, dotnet-gcdump.

Specifically, programs create a socket in `/tmp` named `dotnet-diagnostic-{pid}-{disambiguator}-socket`

Microsoft has [documented](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_enablediagnostics) the feature
for their .NET runtime, .NET SDK and .NET CLI software.

They neglected to document the feature is viral.
In addition to the .NET SDK, it is included into all binaries compiled using the SDK.
The feature is enabled by default even when the compiled ELF binary is deployed to a computer
which has neither .NET runtime, .NET SDK nor .NET CLI installed.

To mitigate, set the following environment variable before running any programs compiled with the .NET SDK, and the socket should be no more.

```sh
export DOTNET_EnableDiagnostics=0
```