using System.Security.Cryptography;
using System.Text.Json;
namespace AcmeV2;

sealed class AcmeAccount: iAcmeAccount
{
	readonly AcmeWebClient client;
	readonly string clientId;
	readonly ECDsa dsa;

	public AcmeAccount( AcmeWebClient client, AccountInfo info )
	{
		this.client = client;
		dsa = info.dsa();
		clientId = info.id;
	}

	async Task<OrderResponse> orderRequest( string[] names, CancellationToken cancel )
	{
		Dictionary<string, int> dict = new( names.Length, StringComparer.OrdinalIgnoreCase );
		for( int i = 0; i < names.Length; i++ )
		{
			if( !dict.TryAdd( names[ i ], i ) )
				throw new ArgumentException( "Duplicated names are not supported" );
		}

		string url = client.endpoints.newOrder;
		string nonce = await client.getNewNonce( cancel );

		var header = new Json.ProtectedHeader( clientId, nonce, url );
		var payload = new Json.NewOrderRequest( names );
		var signed = Json.Signed.sign( dsa, header, payload );

		(byte[] bytes, string location) = await client.postNewOrder( url, signed, cancel );
		Json.NewOrderResponse response = JsonSerializer.Deserialize( bytes, Json.Serialise.Default.NewOrderResponse )!;

		// The order must be restored
		PendingAuth[] arr = new PendingAuth[ names.Length ];
		for( int i = 0; i < response.identifiers.Length; i++ )
		{
			Json.OrderIdentifier id = response.identifiers[ i ];

			if( !dict.TryGetValue( id.value, out int index ) )
				throw new ArgumentException( $"Unexpected identifier in response: {id.value}" );
			dict.Remove( id.value );

			string auth = response.authorizations[ i ];
			arr[ index ] = new PendingAuth( id.value, auth );
		}

		return new OrderResponse( arr, response.finalize, location, response.expires );
	}

	async Task<HttpChallenge> getChallenge( PendingAuth auth, string thumbprint, CancellationToken cancel )
	{
		// Get JSON from the provided URL
		byte[] bytes = await client.get( auth.auth, cancel );
		// Parse the JSON
		Json.ChallengeResponse json = JsonSerializer.Deserialize( bytes, Json.Serialise.Default.ChallengeResponse )!;

		// Find the "http-01" entry we're after
		var e = json.challenges.FirstOrDefault( e => e.type == "http-01" );
		if( null == e )
			throw new ArgumentException( "The server failed to produce \"http-01\" challenge" );

		// Produce a strongly typed readonly structure
		string content = $"{e.token}.{thumbprint}";
		return new HttpChallenge( auth.domain, e.url, e.token, content, json.expires );
	}

	async Task<OrderChallenges> iAcmeAccount.newOrder( string[] names, CancellationToken cancel )
	{
		// POST to newOrder, parse JSON, transform string soup into a strongly typed readonly structure
		OrderResponse order = await orderRequest( names, cancel );

		// Compute thumbprint of the public ECC key we use to sign stuff
		string thumbprint = AcmeUtils.accountThumbprint( dsa );

		// Parse challenges JSONs, all at once in parallel
		Task<HttpChallenge>[] tasks = new Task<HttpChallenge>[ order.auth.Length ];
		for( int i = 0; i < order.auth.Length; i++ )
			tasks[ i ] = getChallenge( order.auth[ i ], thumbprint, cancel );

		// Wait for results to arrive
		HttpChallenge[] results = await Task.WhenAll( tasks );

		// Return another strongly typed readonly structure
		return new OrderChallenges( order.finalize, order.location, results, order.expires );
	}

	async Task validateChallenge( string url, long timeout, CancellationToken cancel )
	{
		while( true )
		{
			string nonce = await client.getNewNonce( cancel );

			var header = new Json.ProtectedHeader( clientId, nonce, url );
			var req = Json.Signed.signHeader( dsa, header, Json.eSignedHeaderFlavour.EmptyJson );
			byte[] bytes = await client.post( url, req, cancel );
			// Parse the JSON
			Json.PollResponse json = JsonSerializer.Deserialize( bytes, Json.Serialise.Default.PollResponse )!;
			if( json.status.success() )
				return;

			if( json.status.pending() )
			{
				if( Environment.TickCount64 > timeout )
					throw new TimeoutException();
				await Task.Delay( 256 );
				continue;
			}

			string message = AcmeUtils.failMessage( json.status, json.error );
			throw new ApplicationException( message );
		}
	}

	Task iAcmeAccount.validateChallenges( string[] urls, TimeSpan timeout )
	{
		if( urls.Length == 0 || timeout.Ticks <= 0 )
			throw new ArgumentException();

		using CancellationTokenSource cts = new();
		cts.CancelAfter( timeout + TimeSpan.FromMilliseconds( 256 ) );
		CancellationToken cancel = cts.Token;

		long timeoutElapse = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
		return Task.WhenAll( urls.Select( u => validateChallenge( u, timeoutElapse, cancel ) ) );
	}

	static void throwIfFailed( Json.CertificateResponse response )
	{
		if( !response.status.failed() )
			return;
		string message = AcmeUtils.failMessage( response.status, response.error );
		throw new ApplicationException( message );
	}

	async Task<Json.CertificateResponse> finalizeOrder( string url, string[] domains, ECDsa key, CancellationToken cancel )
	{
		byte[] csr = AcmeUtils.signingRequest( key, domains );

		string nonce = await client.getNewNonce( cancel );
		var header = new Json.ProtectedHeader( clientId, nonce, url );
		var payload = new Json.CertificateRequest( csr );
		var signed = Json.Signed.sign( dsa, header, payload );
		byte[] bytes = await client.post( url, signed, cancel );
		var response = JsonSerializer.Deserialize( bytes, Json.Serialise.Default.CertificateResponse )!;
		throwIfFailed( response );
		return response;
	}

	async Task<Json.CertificateResponse> orderStatus( string location, CancellationToken cancel )
	{
		string nonce = await client.getNewNonce( cancel );
		var header = new Json.ProtectedHeader( clientId, nonce, location );
		var signed = Json.Signed.signHeader( dsa, header, Json.eSignedHeaderFlavour.EmptyString );
		byte[] bytes = await client.post( location, signed, cancel );
		var response = JsonSerializer.Deserialize( bytes, Json.Serialise.Default.CertificateResponse )!;
		throwIfFailed( response );
		return response;
	}

	async Task<OrderStatus> iAcmeAccount.issueCertificate( string finalize, string location,
		string[] domains, ECDsa key, CancellationToken cancel )
	{
		// Check if already available
		Json.CertificateResponse response = await orderStatus( location, cancel );
		if( response.status.success() && null != response.certificate )
			return new OrderStatus( response );

		// Finalize
		Debug.Assert( response.finalize == finalize );
		response = await finalizeOrder( finalize, domains, key, cancel );
		if( response.status.success() )
			return new OrderStatus( response );

		// Poll for completion
		const int iter = 32;
		const int msDelay = 2048;
		for( int i = 1; i < iter; i++ )
		{
			await Task.Delay( msDelay );
			response = await orderStatus( location, cancel );
			if( response.status.success() )
				return new OrderStatus( response );
		}
		throw new TimeoutException();
	}

	Task iAcmeAccount.deactivate( CancellationToken cancel )
	{
		throw new NotImplementedException();
	}

	void IDisposable.Dispose() => dsa.Dispose();
}