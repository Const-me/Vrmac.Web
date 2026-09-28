using MaxMind.Db;
namespace GeoIpTest.Records;

record Record
{
	public Continent? continent { get; init; }
	public Country? country { get; init; }
	public Country? regCountry { get; init; }

	[Constructor]
	public Record( [MapKey( "continent" )] Continent? continent = null,
		[MapKey( "country" )] Country? country = null,
		[MapKey( "registered_country" )] Country? regCountry = null )
	{
		this.continent = continent;
		this.country = country;
		this.regCountry = regCountry;
	}
}