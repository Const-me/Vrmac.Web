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