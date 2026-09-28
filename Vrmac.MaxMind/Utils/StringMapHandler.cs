namespace MaxMind.Db;

/// <summary>De-serialisation handler for string to string dictionary</summary>
sealed class StringMapHandler: iTypeHandler
{
	public static void register( DecoderAot decoder ) =>
		decoder.addTypeHandler( typeof( Dictionary<string, string> ), instance );

	private StringMapHandler() { }
	static readonly iTypeHandler instance = new StringMapHandler();

	object iTypeHandler.create( IReadOnlyDictionary<Key, object> fields )
	{
		Dictionary<string, string> dict = new Dictionary<string, string>( fields.Count );
		foreach( var kvp in fields )
			dict.Add( kvp.Key.ToString(), (string)kvp.Value );
		return dict;
	}
	Type? iTypeHandler.fieldType( in Key key ) => typeof( string );
	bool iTypeHandler.cacheAllValues => false;
}