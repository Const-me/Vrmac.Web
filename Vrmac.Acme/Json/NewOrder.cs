using System.Text.Json;
using System.Text.Json.Serialization;
namespace AcmeV2.Json;

sealed class OrderIdentifier
{
	[JsonInclude]
	public string type, value;

	public OrderIdentifier() => type = value = "";
	public OrderIdentifier( string value )
	{
		type = "dns";
		this.value = value;
	}
}

sealed record class NewOrderRequest: iSerialise
{
	[JsonInclude]
	public OrderIdentifier[] identifiers;

	public NewOrderRequest() =>
		identifiers = Array.Empty<OrderIdentifier>();

	public NewOrderRequest( string[] names )
	{
		identifiers = new OrderIdentifier[ names.Length ];
		for( int i = 0; i < names.Length; i++ )
			identifiers[ i ] = new OrderIdentifier( names[ i ] );
	}

	public string json() => JsonSerializer.Serialize( this, Serialise.Default.NewOrderRequest );
}


sealed record class NewOrderResponse
{
	[JsonInclude]
	public eStatus status;

	[JsonInclude]
	public string finalize;

	[JsonInclude]
	public OrderIdentifier[] identifiers;

	[JsonInclude]
	public string[] authorizations;

	[JsonInclude]
	public string? expires;

	public NewOrderResponse()
	{
		status = default;
		finalize = "";
		identifiers = Array.Empty<OrderIdentifier>();
		authorizations = Array.Empty<string>();
		expires = null;
	}
}