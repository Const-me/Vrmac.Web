// The following macro should stay commented out in production builds
// #define DBG_SAVE_FAILS
using System.Text.Json;
namespace AcmeV2;

sealed class AcmeWebClient: IDisposable
{
	public static async Task<AcmeWebClient> create( string directory, CancellationToken cancel )
	{
		HttpClient http = new();
		try
		{
			Endpoints endpoints = await Endpoints.fetch( http, directory, cancel );
			return new AcmeWebClient( http, endpoints );
		}
		catch
		{
			http.Dispose();
			throw;
		}
	}

	static string responseNonce( HttpResponseMessage message ) =>
		message.Headers.GetValues( "Replay-Nonce" ).First();

	public async Task<string> getNewNonce( CancellationToken cancel )
	{
		using HttpRequestMessage req = new HttpRequestMessage( HttpMethod.Head, endpoints.newNonce );
		using HttpResponseMessage response = await http.SendAsync( req, cancel );
		response.EnsureSuccessStatusCode();
		return responseNonce( response );
	}

	public async Task<NewAccount> newAccount( Json.Signed signed, CancellationToken cancel )
	{
		using var rented = buffers.rent();
		using HttpContent content = rented.buffer.write( signed );
		using HttpResponseMessage message = await http.PostAsync( endpoints.newAccount, content, cancel );

#if DBG_SAVE_FAILS
		if( !message.IsSuccessStatusCode )
		{
			byte[] err = await message.Content.ReadAsByteArrayAsync( cancel );
			File.WriteAllBytes( @"C:\Temp\2remove\Web\fail.json", err );
		}
#endif
		message.EnsureSuccessStatusCode();

		Json.NewAccountResponse json;
		byte[] body = await message.Content.ReadAsByteArrayAsync( cancel );
		json = JsonSerializer.Deserialize( body, Json.Serialise.Default.NewAccountResponse )!;

		if( json.status != eStatus.Valid )
			throw new ArgumentException( $"New account response status: {json.status}" );

		string id = message.Headers.GetValues( "Location" ).First();
		string orders = json.orders!;
		return new NewAccount( id, orders );
	}

	public async Task<byte[]> get( string url, CancellationToken cancel )
	{
		using HttpResponseMessage message = await http.GetAsync( url, cancel );
		message.EnsureSuccessStatusCode();
		return await message.Content.ReadAsByteArrayAsync( cancel );
	}

	public async Task<byte[]> post( string url, Json.Signed signed, CancellationToken cancel )
	{
		using var rented = buffers.rent();
		using HttpContent content = rented.buffer.write( signed );
		using HttpResponseMessage message = await http.PostAsync( url, content, cancel );
#if DBG_SAVE_FAILS
		if( !message.IsSuccessStatusCode )
		{
			byte[] err = await message.Content.ReadAsByteArrayAsync( cancel );
			File.WriteAllBytes( @"C:\Temp\2remove\Web\fail.json", err );
		}
#endif
		message.EnsureSuccessStatusCode();
		byte[] body = await message.Content.ReadAsByteArrayAsync( cancel );
		return body;
	}

	public async Task<(byte[], string)> postNewOrder( string url, Json.Signed signed, CancellationToken cancel )
	{
		using var rented = buffers.rent();
		using HttpContent content = rented.buffer.write( signed );
		using HttpResponseMessage message = await http.PostAsync( url, content, cancel );
		message.EnsureSuccessStatusCode();

		string location = extractLocation( message ) ?? throw new InvalidOperationException( "ACME newOrder response missing Location header" );
		byte[] body = await message.Content.ReadAsByteArrayAsync( cancel );
		return (body, location);

		static string? extractLocation( HttpResponseMessage message )
		{
			IEnumerable<string>? vals;
			if( !message.Headers.TryGetValues( "Location", out vals ) )
				return null;
			return vals.FirstOrDefault();
		}
	}

	AcmeWebClient( HttpClient http, Endpoints endpoints )
	{
		this.http = http;
		this.endpoints = endpoints;
	}

	readonly HttpClient http;
	readonly RequestBuffers buffers = new();
	public readonly Endpoints endpoints;

	public void Dispose() => http.Dispose();
}