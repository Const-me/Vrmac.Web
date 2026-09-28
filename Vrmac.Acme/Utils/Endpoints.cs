#pragma warning disable CS0649 // Field is never assigned to
namespace AcmeV2;

/// <summary>ACME v2 directory</summary>
sealed record class Endpoints
{
	/// <summary>New nonce</summary>
	public readonly string newNonce;
	/// <summary>New account</summary>
	public readonly string newAccount;
	/// <summary>New order</summary>
	public readonly string newOrder;
	/// <summary>New authorization</summary>
	public readonly string? newAuthz;
	/// <summary>Revoke certificate</summary>
	public readonly string revokeCert;
	/// <summary>Key change</summary>
	public readonly string keyChange;

	Endpoints( Json.Endpoints json )
	{
		newNonce = json.newNonce!;
		newAccount = json.newAccount!;
		newOrder = json.newOrder!;
		// If the ACME server does not implement pre-authorization (Section 7.4.1), it MUST omit the "newAuthz" field of the directory.
		newAuthz = json.newAuthz;
		revokeCert = json.revokeCert!;
		keyChange = json.keyChange!;
	}

	public static async Task<Endpoints> fetch( HttpClient httpClient, string url, CancellationToken cancel )
	{
		Json.Endpoints json = await httpClient.getJson( url, Json.Serialise.Default.Endpoints, cancel );
		return new Endpoints( json );
	}
}