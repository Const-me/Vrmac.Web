using System.Security.Cryptography;
namespace AcmeV2;

/// <summary>ACME v2 client session</summary>
/// <remarks>Call <see cref="ACME.create(string, CancellationToken)" /> factory function to create the object.<br/>
/// The object owns HttpClient; please dispose once no longer needed.</remarks>
public interface iAcmeClient: IDisposable
{
	/// <summary>Register a new account, return client ID</summary>
	Task<AccountInfo> register( string[] contact, CancellationToken cancel );

	/// <summary>Create session object for the previously registered account</summary>
	iAcmeAccount account( AccountInfo info );

	/// <summary>Download a newly issued certificate, save all of them (chain included) into a custom binary container</summary>
	/// <remarks><c>certPublic</c> should contain public key passed to <see cref="iAcmeAccount.issueCertificate" />.
	/// The parameter is used to distinguish leaf certificate[s] from the chain,
	/// because let’s encrypted failed to document the order of the certificates they are generating.</remarks>
	Task downloadCertificate( string savePath, OrderStatus completedOrder, ECParameters certPublic, CancellationToken cancel );
}

/// <summary>ACME v2 account</summary>
/// <remarks>Make sure iAcmeClient who created the account stays alive i.e. not disposed for as long as you are using this object.</remarks>
public interface iAcmeAccount: IDisposable
{
	/// <summary>Creates an ACME v2 order</summary>
	/// <remarks>That’s the first step of obtaining a new certificate with the ACME protocol from the certificate authority.<br/>
	/// The next step is validating ownership of these domains.</remarks>
	Task<OrderChallenges> newOrder( string[] names, CancellationToken cancel );

	/// <summary>Validate ownership of your domain[s]. May take a while to complete.</summary>
	/// <remarks>Before calling this method, make sure to serve the magic tokens from <see cref="OrderChallenges.challenges" />
	/// at the <c>/.well-known/acme-challenge/{token}</c></remarks>
	/// <seealso cref="ACME.validateChallenges(iAcmeAccount, in OrderChallenges, TimeSpan)" />
	Task validateChallenges( string[] urls, TimeSpan timeout );

	/// <summary>Issue the new certificate.</summary>
	/// <remarks>Before calling this method, make sure <see cref="validateChallenges"/> completed successfully.<br/>
	/// The ECDSA object must contain private key for the sertificate: this method uses it to sign stuff.</remarks>
	/// <seealso cref="ACME.issueCertificate(iAcmeAccount, in OrderChallenges, ECDsa, CancellationToken)"/>
	Task<OrderStatus> issueCertificate( string finalize, string location,
		string[] domains, ECDsa key, CancellationToken cancel );

	/// <summary>Deactivate the account</summary>
	/// <remarks>The method is untested</remarks>
	Task deactivate( CancellationToken cancel );
}