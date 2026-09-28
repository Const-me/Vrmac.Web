using GeoIpTest.Records;
using MaxMind.Db;
using System.Net;
using Vrmac.GeoIP;
using Vrmac.MaxMind;
namespace GeoIpTest;

static class LookupExt
{
	/// <summary>Query data from the OG database, resolve into the region using classes imported from Vrmac.MaxMind.dll</summary>
	public static eRegion lookup( this Reader reader, IPAddress ip )
	{
		Record? rec = reader.Find<Record>( ip );
		if( null == rec )
			return eRegion.Unassigned;

		CountryCode? c1 = rec.country?.isoCode;
		CountryCode? c2 = rec.regCountry?.isoCode;
		CountryCode countryCode = c1 ?? c2 ?? throw new ArgumentException();
		eContinent continent = rec.continent?.code ?? eContinent.Unknown;

		RegionLookup lookup = RegionLookup.lookup( countryCode );
		if( lookup.continent == continent || continent == eContinent.Unknown )
			return lookup.id;
		return regionAuto( continent );
	}

	/// <summary>Derive a region from continent only</summary>
	static eRegion regionAuto( eContinent cont ) => cont switch
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