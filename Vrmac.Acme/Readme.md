# Vrmac.Acme

This library implements a .NET 10 client for
[ACME v2](https://en.wikipedia.org/wiki/Automatic_Certificate_Management_Environment#API_version_2) protocol.

The library is compatible with [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/?tabs=windows%2Cnet9plus),
tested with `TrimMode=Full` trimming option.

For many websites, an expired TLS certificate is an epic fail. 
Make sure to log exceptions thrown by this library rigorously, retry things which fail, 
log successful renewals, and consider external monitoring.

Although this library has been running flawlessly on my website for 9 months and counting, I can’t guarantee it’s standard compliant or bug free.
Like any other third-party web service let’s encrypt has technical ability to break things on their side without notice.
Lastly, no network is 100% reliable.

When evaluating and testing things, consider let’s encrypt [staging environment](https://letsencrypt.org/docs/staging-environment/).
However, based on my tests these environments are behaving differently; I ran into differences in server logic, not just rate limits.
Don’t assume a working test with staging guarantees great success with the production ACME server.

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
albeit none of that should matter as this library is written in idiomatic memory-safe C# without native interop or platform intrinsics.

The library only supports [http-01](https://www.rfc-editor.org/info/rfc8555/#section-8.3) verification.
If you need wildcard certificates,
consider forking this library adding support for [dns-01](https://www.rfc-editor.org/info/rfc8555/#section-8.4) verification.

## Technical details

The public API is based on async-await.
The implementation relies on the thread pool implemented by the .NET runtime.

All cryptography stuff is from the .NET 10 standard library.
All JSON stuff is implemented using a [source generator](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
from the standard library.

Despite the [ACME v2 spec](https://www.rfc-editor.org/info/rfc8555/) is almost 100 pages long,
the authors didn’t do a great job documenting input validation and error handling.
See `Signed.signHeader<Header>` method for an illustration.

## Integration

The main entry point of the library is `AcmeV2.ACME` static class.

Perhaps the most important member of that class is `create` factory function.
The function fetches and parses [directory](https://www.rfc-editor.org/info/rfc8555/#section-7.1.1) with endpoints,
and returns an object which acts as a callable proxy for the JSON RPC APIs implemented by the server.

Here are the steps necessary to obtain and renew TLS certificates.

For simplicity, the examples are using `CancellationToken.None`.
For production use, you’d want to pass a better cancellation token.

### ACME Directory

Create client object like that:

```C#
using acmeClient = await ACME.create( "https://acme-v02.api.letsencrypt.org/directory", CancellationToken.None );
```

### Initial Setup

I recommend doing the steps in this section on a trusted computer, not on your production server.
Then deploy the account key and the certificate key to production; note the server does not need permissions to write either of these files.

To register a new account, call `await iAcmeClient.register` RPC. The argument is array of contact e-mails.

Call `AccountInfo.serialise()`.
Encrypt the byte array with `SymmetricCrypto.encrypt` as it contains a private key, and ave the encrypted array somewhere.
You will need that data every time you renew the certificates.

To generate a new certificate key, call `ACME.generateCertificateKey()`.
Export the private key with [ExportECPrivateKey](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.ecalgorithm.exportecprivatekey?view=net-10.0#system-security-cryptography-ecalgorithm-exportecprivatekey) method.
Encrypt the bytes with `SymmetricCrypto.encrypt`, and save the encrypted array somewhere.
You will need that data every time you renew your certificate, also on each startup of the server.

### Certificate Renewal

Load account key from disk, decrypt with `SymmetricCrypto.decrypt`,
parse with `AccountInfo.deserialise`, and call `iAcmeClient.account`.
This will get you the `iAcmeAccount` object.

Submit a new order passing DNS names of your domains:

```C#
OrderChallenges oc = await acmeAccount.newOrder(...)
```

Serve the magic strings from OrderChallenges.challenges field at the `/.well-known/acme-challenge/{token}` URL of these domains.
Here’s an example, call the `setupChallenges` method to supply the data.

```C#
/// <summary>Utility class to serve ACME V2 challenges with Kestrel</summary>
sealed class AcmeChallenges
{
	public AcmeChallenges() { }

	/// <summary>Call on startup to setup the route</summary>
	public void map( WebApplication app ) =>
		app.MapGet( "/.well-known/acme-challenge/{token}", serveChallenge );

	volatile IReadOnlyDictionary<string, byte[]>? dict = null;

	/// <summary>Setup payload data to serve</summary>
	public void setupChallenges( IEnumerable<(string, string)> list )
	{
		Dictionary<string, byte[]> dict = new();
		foreach( (string k, string v) in list )
			dict[ k ] = Encoding.UTF8.GetBytes( v );

		dict.TrimExcess();
		Interlocked.Exchange( ref this.dict, dict );
	}

	/// <summary>Clear the payload data</summary>
	public void clearChallenges() =>
		Interlocked.Exchange( ref dict, null );

	ValueTask serveChallenge( string token, HttpContext context )
	{
		HttpResponse response = context.Response;
		IReadOnlyDictionary<string, byte[]>? dict = this.dict;
		byte[]? payload;

		if( null == dict || !dict.TryGetValue( token, out payload ) )
		{
			response.StatusCode = StatusCodes.Status404NotFound;
			return ValueTask.CompletedTask;
		}

		return response.sendText( payload );
	}
}
```

The extension method:

```C#
/// <summary>Send the bytes in plain text response</summary>
public static ValueTask sendText( this HttpResponse response, byte[] content )
{
	response.StatusCode = StatusCodes.Status200OK;
	response.ContentType = "text/plain";
	response.ContentLength = content.Length;
	return response.Body.WriteAsync( content );
}
```

Validate the domains:

```C#
await acmeAccount.validateChallenges( oc, someTimeout );
```

After that function completes successfully, you may clear the challenges.

Issue the new certificate:

```C#
OrderStatus status = await acmeAccount.issueCertificate( oc, certificateKey, CancellationToken.None );
```

Save the certificate to disk.

```C#
await acmeClient.downloadCertificate( path, status, certPublic, CancellationToken.None );
```

The `string savePath` argument is absolute path to the destination file.
The server needs permissions to create, write and rename files in the directory containing that file.

The `ECParameters certPublic` argument must be public key from the certificateKey.

### Certificate Loading

Call `ACME.loadCertificates` function.
It takes 2 arguments: path to the certificate file, and the key.
The ECDsa parameter must include the private key.

The output structure contains both the certificate chain, and expiration date:

```C#
/// <summary>A container of certificates loaded from the custom binary format</summary>
public readonly struct Certificates
{
	/// <summary>Expiration date of the certificate</summary>
	/// <remarks>Need that field to figure out when it's time to renew</remarks>
	public readonly DateTime expiration;

	/// <summary>Certificates in the container.</summary>
	/// <remarks>The first one is the leaf, and is guaranteed to have the private key.</remarks>
	public readonly X509Certificate2[] certs;

	internal Certificates( DateTime expiration, X509Certificate2[] certs )
	{
		this.expiration = expiration;
		this.certs = certs;
	}
}
```