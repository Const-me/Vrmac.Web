# Vrmac.Security

This library implements some higher-level cryptography-related functions on top of the .NET 10 standard library.

`SecretBytes` structure implements a [RAII](https://en.wikipedia.org/wiki/Resource_acquisition_is_initialization) wrapper around array of bytes. 
It calls [ZeroMemory](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicoperations.zeromemory?view=net-10.0) when disposed.

`SymmetricCrypto` static class implements password-based encrypt/decrypt operations backed by AES-256 GCM.
Designed to encrypt small blobs of bytes stored on disk.

`BlobContainer` static class implements functions to pack and unpack multiple byte blobs to/from a single one.

`BackupStream` class implements a one-way backup encryption scheme using [ECDH](https://en.wikipedia.org/wiki/Elliptic-curve_Diffie%E2%80%93Hellman) 
key pairs on the NIST P-521 curve.\
Call `BackupKeyPair.generate` function to generate a new key pair.
Keep the private key away from the machine that produces backups, ideally completely offline.\
Use the `BackupDecrypt` static class to decrypt previously encrypted files.

The BackupStream is designed for automatic daily backups where the server only has the public half of the key, 
so it can’t read or modify the content of previously encrypted backups even if someone finds
an [RCE](https://en.wikipedia.org/wiki/Arbitrary_code_execution) bug in ASP.NET Core runtime.