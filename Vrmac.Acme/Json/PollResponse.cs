using System.Text.Json.Serialization;
namespace AcmeV2.Json;

sealed class Problem
{
	[JsonInclude]
	public string? type, detail;
	[JsonInclude]
	public int status;
}

sealed class PollResponse
{
	[JsonInclude]
	public OrderIdentifier? identifier = null;

	[JsonInclude]
	public eStatus status = default;

	[JsonInclude]
	public string? expires = null;

	[JsonInclude]
	public Problem? error;
}