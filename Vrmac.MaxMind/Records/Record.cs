using MaxMind.Db;
namespace Vrmac.MaxMind;

record Record
{
	public Continent? continent { get; init; }
	public Country? country { get; init; }
	public Country? regCountry { get; init; }

	public Record( Continent? continent = null, Country? country = null, Country? regCountry = null )
	{
		this.continent = continent;
		this.country = country;
		this.regCountry = regCountry;
	}
}

sealed class RecordHandler: iTypeHandler
{
	public static void register( DecoderAot decoder )
	{
		decoder.addTypeHandler( typeof( Record ), instance );
		ContinentHandler.register( decoder );
		CountryHandler.register( decoder );
	}

	readonly Key continent = new Key( "continent"u8 );
	readonly Key country = new Key( "country"u8 );
	readonly Key regCountry = new Key( "registered_country"u8 );

	Type? iTypeHandler.fieldType( in Key key )
	{
		if( key == continent )
			return typeof( Continent );
		if( key == country || key == regCountry )
			return typeof( Country );
		return null;
	}

	object iTypeHandler.create( IReadOnlyDictionary<Key, object> fields )
	{
		Continent? cont = fields.GetValueOrDefault( continent ) as Continent;
		Country? c1 = fields.GetValueOrDefault( country ) as Country;
		Country? c2 = fields.GetValueOrDefault( regCountry ) as Country;
		return new Record( cont, c1, c2 );
	}
	bool iTypeHandler.cacheAllValues => false;

	private RecordHandler() { }
	static readonly iTypeHandler instance = new RecordHandler();
}