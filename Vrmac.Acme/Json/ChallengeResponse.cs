using System.Text.Json.Serialization;
namespace AcmeV2.Json;

sealed class ChallengeResponse
{
	[JsonInclude]
	public OrderIdentifier? identifier = null;

	[JsonInclude]
	public eStatus status = default;

	[JsonInclude]
	public string? expires;

	public sealed class Entry
	{
		[JsonInclude]
		public string type, url, token;

		[JsonInclude]
		public eStatus status = default;

		public Entry() => type = url = token = string.Empty;
	}

	[JsonInclude]
	public Entry[] challenges = Array.Empty<Entry>();
}