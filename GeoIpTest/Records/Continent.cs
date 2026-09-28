using MaxMind.Db;
using Vrmac.MaxMind;
namespace GeoIpTest.Records;

record Continent
{
	public eContinent code { get; init; }
	public string? name { get; init; }

	[Constructor]
	public Continent( string code, IReadOnlyDictionary<string, string>? names = null )
	{
		this.code = dict[ code ];
		name = names?.GetValueOrDefault( "en" );
	}

	static readonly Dictionary<string, eContinent> dict;

	static Continent()
	{
		dict = new Dictionary<string, eContinent>( StringComparer.OrdinalIgnoreCase )
		{
			{ "AF", eContinent.Africa },
			{ "AN", eContinent.Antarctica },
			{ "AS", eContinent.Asia },
			{ "EU", eContinent.Europe },
			{ "OC", eContinent.Oceania },
			{ "SA", eContinent.SouthAmerica },
			{ "NA", eContinent.NorthAmerica },
		};
	}
}