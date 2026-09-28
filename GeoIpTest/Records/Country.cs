using MaxMind.Db;
using Vrmac.MaxMind;
namespace GeoIpTest.Records;

record Country
{
	public CountryCode isoCode { get; init; }
	public string? name { get; init; }

	[Constructor]
	public Country( [MapKey( "iso_code" )] string? isoCode = null,
		[MapKey( "names" )] IReadOnlyDictionary<string, string>? names = null )
	{
		if( !string.IsNullOrEmpty( isoCode ) )
			this.isoCode = new CountryCode( isoCode );
		else
			this.isoCode = CountryCode.invalid;
		name = names?.GetValueOrDefault( "en" );
	}
}