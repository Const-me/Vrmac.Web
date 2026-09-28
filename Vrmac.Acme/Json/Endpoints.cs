using System.Text.Json.Serialization;
namespace AcmeV2.Json;

sealed class Endpoints
{
	[JsonInclude]
	public string? newNonce, newAccount, newOrder, newAuthz, revokeCert, keyChange;
}