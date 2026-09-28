# Vrmac.Uploads

This library implements server side of a resumable file upload protocol.

## The protocol

The uploads are anonymous; requests don’t require authentication.
All request are POST to the single URL, with Content-Type `application/octet-stream` for both requests and responses.
The protocol doesn’t need any custom HTTP headers. The server doesn’t even check Content-Type.
However, the [Content-Length](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Length) header
is required for all requests, and is sent for the responses that contain a non-empty message body.

The files being uploaded are content addressable using their SHA-512 hashes.
On the server, uploaded files are named using [Base64Url](https://learn.microsoft.com/en-us/dotnet/api/System.Buffers.Text.Base64Url?view=net-10.0) of these hashes.

Concurrent upload of a single file is not supported and will fail.
The protocol was designed to upload large encrypted files.
Hence it assumes every file is unique, sans the astronomically small chance of SHA-512 collision.

In the following section, message IDs are [GUIDs](https://learn.microsoft.com/en-us/dotnet/api/system.guid?view=net-10.0)
injected into this library using the corresponding readonly properties of your `iUploaderHost` object.\
The interface should return stable, hardcoded values which match your client-side code.

**Protip:** for optimal obscurity, use fake GUIDs made from the bytes generated at design time
using [RNG](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.fill?view=net-10.0).

### Upload or resume request
* 32 bytes SHA-256 of the rest of the request body
* 16 bytes hardcoded message ID "UploadRequest"
* 16 bytes nonce, which should be cryptography-grade random bytes
* 64 bytes SHA-512 of the full payload
* 8 bytes full payload length, int64

### Upload or resume response

HTTP 200 OK status

* 32 bytes SHA-256 of the rest of the response
* 16 bytes hardcoded message ID "UploadResponse"
* 8 bytes read offset, int64: zero for a new upload,
sum of payload lengths of all previously received valid chunks when resuming.
For a completed upload, the number is equal to the full payload length.
* 2 bytes signature length, uint16
* ECDSA signature of the nonce enabling the client to verify authenticity of the server.

### Upload chunk request
* 32 bytes SHA-256 of the rest of the request body, including the variable-length chunk payload
* 16 bytes hardcoded message ID "ChunkRequest"
* 64 bytes SHA-512 of the full payload (not just the current chunk)
* 8 bytes upload offset, int64; the server validates the number meticulously.
It must be equal to the upload offset immediately preceding this chunk.
* 4 bytes chunk length, int32; must be in \[ 1 .. 2^21 \] interval, see `UploaderProto.maxChunk` for the magic number.
* The chunk payload

### Upload chunk response
Successful response when the server is expecting moar chunks of the file being uploaded:\
HTTP 204 No Content, empty body

Successful response for completed upload:\
HTTP 201 Created, empty body

### Miscellaneous

When things fail, the server may return HTTP 400, 404, 409, 413, 500, 507, etc.

The server disables all caches for all responses.

## Tips and Tricks

Make sure the uploaded files are stored in a location outside static content root or any other mapped directory.
Failing to do so will eventually ban your web server from the internets,
after someone finds out and uses your server to distribute illegal content.

Make sure the `UploaderLimits.maxDiskSpace` value returned from your `iUploaderHost.limits` is substantially smaller
than disk capacity. Modern operating systems tend to fail spectacularly when their disk is full.

If you are managing your server[s] remotely with a CLI tool,
consider [SFTP](https://en.wikipedia.org/wiki/SSH_File_Transfer_Protocol) protocol to download these files,
as opposed to the barebone [SCP](https://en.wikipedia.org/wiki/Secure_copy_protocol).
Modern [OpenSSH](https://en.wikipedia.org/wiki/OpenSSH) servers,
as well as [SSH.NET](https://www.nuget.org/packages/SSH.NET) client library, support both out of the box.
SFTP delivers better usability for the use case as it can list and delete files, not just copy them across the network.