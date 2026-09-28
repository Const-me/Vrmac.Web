using MaxMind.Db;
namespace Vrmac.MaxMind;

record Country
{
	public CountryCode isoCode { get; init; }
	public string? name { get; init; }

	public Country( string? isoCode = null, IReadOnlyDictionary<string, string>? names = null )
	{
		if( !string.IsNullOrEmpty( isoCode ) )
			this.isoCode = new CountryCode( isoCode );
		else
			this.isoCode = CountryCode.invalid;
		name = names?.GetValueOrDefault( "en" );
	}
}

sealed class CountryHandler: iTypeHandler
{
	public static void register( DecoderAot decoder )
	{
		decoder.addTypeHandler( typeof( Country ), instance );
		StringMapHandler.register( decoder );
	}

	readonly Key code = new Key( "iso_code"u8 );
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
		string? cc = fields.GetValueOrDefault( code ) as string;
		Dictionary<string, string>? dict = fields.GetValueOrDefault( names ) as Dictionary<string, string>;
		return new Country( cc, dict );
	}
	bool iTypeHandler.cacheAllValues => true;

	private CountryHandler() { }
	static readonly iTypeHandler instance = new CountryHandler();
}