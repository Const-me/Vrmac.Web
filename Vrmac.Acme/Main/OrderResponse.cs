namespace AcmeV2;

readonly struct PendingAuth
{
	public readonly string domain;
	public readonly string auth;
	internal PendingAuth( string domain, string auth )
	{
		this.domain = domain;
		this.auth = auth;
	}
}

readonly struct OrderResponse
{
	public readonly PendingAuth[] auth;
	public readonly string finalize;
	public readonly string location;
	public readonly DateTime? expires;

	internal OrderResponse( PendingAuth[] auth, string finalize, string location, string? expires )
	{
		this.auth = auth;
		this.finalize = finalize;
		this.location = location;
		this.expires = AcmeUtils.parseDateTime( expires );
	}
}