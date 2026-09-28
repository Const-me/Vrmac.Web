using MaxMind.Db;
namespace Vrmac.MaxMind;

record Continent
{
	public eContinent code { get; init; }
	public string? name { get; init; }

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

sealed class ContinentHandler: iTypeHandler
{
	public static void register( DecoderAot decoder )
	{
		decoder.addTypeHandler( typeof( Continent ), instance );
		StringMapHandler.register( decoder );
	}

	readonly Key code = new Key( "code"u8 );
	readonly Key names = new Key( "names"u8 );

	Type? iTypeHandler.fieldType( in Key key )
	{
		if( key == code )
			return typeof( string );
		if( key == names )
			return typeof( Dictionary<string, string> );
		return null;
	}

	object iTypeHandler.create( IReadOnlyDictionary<Key, object> fields )
	{
		string cc = (string)fields[ code ];
		Dictionary<string, string>? dict = fields.GetValueOrDefault( names ) as Dictionary<string, string>;
		return new Continent( cc, dict );
	}
	bool iTypeHandler.cacheAllValues => true;

	private ContinentHandler() { }
	static readonly iTypeHandler instance = new ContinentHandler();
}