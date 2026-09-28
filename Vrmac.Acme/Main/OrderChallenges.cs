namespace AcmeV2;

/// <summary>One challenge from lets encrypt for a specific domain name</summary>
public readonly struct HttpChallenge
{
	/// <summary>The name passed to <see cref="iAcmeAccount.newOrder" /></summary>
	public readonly string domain;

	/// <summary>Post URL from challenges/url field</summary>
	public readonly string url;

	/// <summary>Token field from challenges/token field</summary>
	public readonly string token;

	/// <summary>The text they want to appear at the following URL:<br/><c>http://{domain}/.well-known/acme-challenge/{token}</c></summary>
	public readonly string content;

	/// <summary><c>"expires"</c> field from the challenge</summary>
	public readonly DateTime? expires;

	internal HttpChallenge( string domain, string url, string token, string content, string? expires )
	{
		this.domain = domain;
		this.url = url;
		this.token = token;
		this.content = content;
		this.expires = AcmeUtils.parseDateTime( expires );
	}
}

/// <summary>Challenges from lets encrypt</summary>
public readonly struct OrderChallenges
{
	/// <summary>Order finalize URL from the new order response</summary>
	public readonly string finalize;
	/// <summary>Order URL from the Location HTTP header of the new order response</summary>
	public readonly string location;
	/// <summary><c>"http-01"</c> challenges, one for each domain name</summary>
	public readonly HttpChallenge[] challenges;
	/// <summary>Optional <c>"expires"</c> field from the new order response</summary>
	public readonly DateTime? expires;

	internal OrderChallenges( string finalize, string location, HttpChallenge[] challenges, DateTime? expires )
	{
		this.finalize = finalize;
		this.location = location;
		this.challenges = challenges;
		this.expires = expires;
	}
}