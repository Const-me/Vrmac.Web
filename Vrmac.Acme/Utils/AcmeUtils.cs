using Microsoft.AspNetCore.WebUtilities;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
namespace AcmeV2;

static class AcmeUtils
{
	public static async Task<T> getJson<T>( this HttpClient httpClient, string url, JsonTypeInfo<T> ti, CancellationToken cancel ) where T : class
	{
		using HttpResponseMessage response = await httpClient.GetAsync( url, HttpCompletionOption.ResponseHeadersRead, cancel );
		response.EnsureSuccessStatusCode();

		using Stream stream = await response.Content.ReadAsStreamAsync( cancel );
		return JsonSerializer.Deserialize( stream, ti )!;
	}

	public static DateTime? parseDateTime( string? str )
	{
		if( string.IsNullOrWhiteSpace( str ) )
			return null;

		const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
		CultureInfo ci = CultureInfo.InvariantCulture;
		// ACME returns dates like "2025-12-01T12:00:00Z"
		if( DateTime.TryParseExact( str, "yyyy-MM-dd'T'HH:mm:ss'Z'", ci, styles, out DateTime dt ) )
			return dt;

		// Attempt general ISO 8601 parse in UTC
		if( DateTime.TryParse( str, ci, styles, out dt ) )
			return dt;

		throw new FormatException( $"Invalid UTC date string: '{str}'" );
	}

	public static string accountThumbprint( ECDsa dsa )
	{
		// 1. Export public parameters
		ECParameters ec = dsa.ExportParameters( false );
		Debug.Assert( ec.Q.X != null && ec.Q.Y != null );

		// 2. Build the JWK JSON with lexicographic property order: crv, kty, x, y
		IEnumerable<(string, string)> properties()
		{
			yield return ("crv", "P-384");
			yield return ("kty", "EC");
			yield return ("x", WebEncoders.Base64UrlEncode( ec.Q.X! ));
			yield return ("y", WebEncoders.Base64UrlEncode( ec.Q.Y! ));
		}
		// Serialize without whitespaces i.e. minified JSON
		IEnumerable<string> elements()
		{
			foreach( (string key, string val) in properties() )
				yield return $"\"{key}\":\"{val}\"";
		}
		StringBuilder sb = new StringBuilder();
		sb.Append( '{' );
		sb.AppendJoin( ',', elements() );
		sb.Append( '}' );
		string jwkJson = sb.ToString();

		// 3. Hash using SHA-256
		using SHA256 sha = SHA256.Create();
		byte[] hash = sha.ComputeHash( Encoding.UTF8.GetBytes( jwkJson ) );

		// 4. Return Base64Url-encoded result
		return WebEncoders.Base64UrlEncode( hash );
	}

	public static byte[] signingRequest( ECDsa key, string[] domains )
	{
		CertificateRequest req = new( $"CN={domains[ 0 ]}", key, HashAlgorithmName.SHA384 );
		SubjectAlternativeNameBuilder sanBuilder = new();
		foreach( var d in domains )
			sanBuilder.AddDnsName( d );
		req.CertificateExtensions.Add( sanBuilder.Build() );
		return req.CreateSigningRequest();
	}

	public static bool success( this eStatus status ) => status == eStatus.Valid;
	public static bool pending( this eStatus status )
	{
		switch( status )
		{
			case eStatus.Pending:
			case eStatus.Processing:
				return true;
			default:
				return false;
		}
	}

	public static bool failed( this eStatus status )
	{
		switch( status )
		{
			case eStatus.Pending:
			case eStatus.Processing:
			case eStatus.Valid:
				return false;
			default:
				return true;
		}
	}

	public static string failMessage( Json.Problem error )
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendFormat( "Status code {0}", error.status );
		if( !string.IsNullOrWhiteSpace( error.type ) )
			sb.AppendFormat( ", type = {0}", error.type );
		if( !string.IsNullOrWhiteSpace( error.detail ) )
			sb.AppendFormat( ": {0}", error.detail );
		return sb.ToString();
	}

	public static string failMessage( eStatus status, Json.Problem? error )
	{
		if( !status.failed() )
			throw new ArgumentException();
		if( status == default )
			return "The server failed to provide any status";
		if( null == error )
			return "Failed status, no other information is available";
		return failMessage( error );
	}

	public static uint dateInDays( this DateTime dateTime )
	{
		long ticks = ( dateTime.ToUniversalTime() - DateTimeOffset.UnixEpoch.UtcDateTime ).Ticks;
		long days64 = ticks / TimeSpan.TicksPerDay; // Note rounding down
		return (uint)days64;
	}

	public static DateTime fromDays( uint days )
	{
		long ticks = TimeSpan.TicksPerDay * days;
		return DateTimeOffset.UnixEpoch.UtcDateTime.AddTicks( ticks );
	}

	public const uint certMagic = 0x726563A3;
}