namespace Vrmac.MaxMind;

static class RegionExt
{
	/// <summary>Convert MaxMind country record into region structure</summary>
	public static RegionLookup region( this Record rec )
	{
		CountryCode? c1 = rec.country?.isoCode;
		CountryCode? c2 = rec.regCountry?.isoCode;
		CountryCode countryCode = c1 ?? c2 ?? throw new ArgumentException();
		eContinent continent = rec.continent?.code ?? eContinent.Unknown;
		RegionLookup lookup = RegionLookup.lookup( countryCode );
		if( lookup.continent == continent || continent == eContinent.Unknown )
			return lookup;
		return new RegionLookup( regionAuto( continent ), continent, $"{continent} fallback" );
	}

	/// <summary>Convert MaxMind country record into region enum</summary>
	public static eRegion regionId( this Record rec ) => rec.region().id;

	/// <summary>Parse continent codes into enum</summary>
	public static eContinent parseContinent( string str ) => str.ToLowerInvariant() switch
	{
		"eu" => eContinent.Europe,
		"af" => eContinent.Africa,
		"sa" => eContinent.SouthAmerica,
		"na" => eContinent.NorthAmerica,
		"oc" => eContinent.Oceania,
		"as" => eContinent.Asia,
		"an" => eContinent.Antarctica,
		_ => throw new ArgumentException()
	};

	/// <summary>Derive a region from continent only</summary>
	public static eRegion regionAuto( eContinent cont ) => cont switch
	{
		eContinent.Europe => eRegion.EU,
		eContinent.Africa => eRegion.SSA,
		eContinent.SouthAmerica => eRegion.SA,
		eContinent.NorthAmerica => eRegion.NAC,
		eContinent.Oceania => eRegion.OC,
		eContinent.Asia => eRegion.Asia,
		_ => throw new NotImplementedException(),
	};
}