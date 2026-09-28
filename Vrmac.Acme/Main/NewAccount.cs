namespace AcmeV2;

readonly struct NewAccount
{
	public readonly string id;
	public readonly string orders;

	public NewAccount( string id, string orders )
	{
		this.id = id;
		this.orders = orders;
	}
}