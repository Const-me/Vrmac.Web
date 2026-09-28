using System.IO.Compression;
using System.Reflection;
namespace Vrmac.MaxMind;

/// <summary>Region data, typically parsed from the TSV resource embedded into this DLL</summary>
public readonly struct RegionLookup
{
	public readonly eRegion id;
	public readonly eContinent continent;
#if DEBUG
	public readonly string name;
#endif

	/// <summary>Resolve country ISO code into region</summary>
	public static RegionLookup lookup( CountryCode code )
	{
		RegionLookup result = lookupTable[ code.packed ];
		if( result.id != eRegion.Unassigned )
			return result;
		throw new KeyNotFoundException();
	}

	public RegionLookup( eRegion id, eContinent continent, string name )
	{
		this.id = id;
		this.continent = continent;
#if DEBUG
		this.name = name;
#endif
	}

	static RegionLookup()
	{
		lookupTable = new RegionLookup[ CountryCode.capacity ];

		// Skip the header row
		foreach( string line in readTable().Skip( 1 ) )
		{
			string[] fields = line.Split( '\t' );
			string key = fields[ 0 ];
			string name = fields[ 1 ];
			eContinent cont = RegionExt.parseContinent( fields[ 2 ] );
			string regString = fields[ 3 ];
			eRegion reg;
			if( string.IsNullOrWhiteSpace( regString ) )
				reg = RegionExt.regionAuto( cont );
			else
				reg = Enum.Parse<eRegion>( regString, true );

			CountryCode cc = new CountryCode( key );
			ref RegionLookup rdi = ref lookupTable[ cc.packed ];
			if( rdi.id != eRegion.Unassigned )
				throw new ApplicationException();
			rdi = new RegionLookup( reg, cont, name );
		}
	}

	static readonly RegionLookup[] lookupTable;

	static IEnumerable<string> readTable()
	{
		const string resourceName = "Vrmac.MaxMind.Transform.regionFromCountry.gz";
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream( resourceName ) ??
			throw new ApplicationException( "Embedded resource missing" );
		using GZipStream unzip = new( stream, CompressionMode.Decompress );
		using var r = new StreamReader( unzip );
		while( true )
		{
			string? line = r.ReadLine();
			if( null == line )
				break;
			if( string.IsNullOrWhiteSpace( line ) )
				continue;
			yield return line;
		}
	}
}