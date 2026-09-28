namespace Vrmac.GeoIP;

/// <summary>Broad geographic region resolved from IP address</summary>
/// <seealso cref="GeoPackage" />
public enum eRegion: byte
{
	/// <summary>The IP is not on the list</summary>
	/// <remarks>Also includes stuff like <c>192.168.*.*</c> block</remarks>
	Unassigned = 0,
	/// <summary>North America and Caribbean</summary>
	/// <remarks>Caribbean region is well connected to the US, many submarine cables</remarks>
	NAC,
	/// <summary>South America</summary>
	SA,
	/// <summary>Europe</summary>
	EU,
	/// <summary>Countries on the south and east sides of the Mediterranean sea</summary>
	/// <remarks>Despite they span over both Africa and Asia continents well connected to the EU, many submarine cables</remarks>
	NA,
	/// <summary>Sub-Saharan Africa</summary>
	/// <remarks>Sahara is a showstopper, no internet infrastructure</remarks>
	SSA,
	/// <summary>Firewalled Russia and Belarus</summary>
	RU,
	/// <summary>Central Asia, Caucasus</summary>
	CA,
	/// <summary>Middle East</summary>
	ME,
	/// <summary>India and a few neighbouring countries like Nepal</summary>
	IN,
	/// <summary>Firewalled China and North Korea</summary>
	CN,
	/// <summary>Southeast Asia</summary>
	SEA,
	/// <summary>Oceania</summary>
	/// <remarks>Includes Christmas Island despite Indian Ocean is technically Asia</remarks>
	OC,
	/// <summary>East Asia</summary>
	/// <remarks>Japan, South Korea, Taiwan, Hong Kong, Macao</remarks>
	EA,
	/// <summary>Catch-all Asia</summary>
	/// <remarks>Thousands of entries; SEA DCs of US companies don't have country in the source DB, registered country = US, yet continent = Asia<br/>
	/// Obviously, counting them into US is horribly wrong, connectivity not even close.</remarks>
	Asia,
	/// <summary>Weird regions like satellites, Antarctica, and military bases in the middle of the ocean</summary>
	Other,
}