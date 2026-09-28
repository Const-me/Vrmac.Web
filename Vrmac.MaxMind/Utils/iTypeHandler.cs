namespace MaxMind.Db;

/// <summary>De-serialisation handler for a specific data type</summary>
/// <remarks><see cref="DecoderAot" /> already handles primitives like numbers and strings internally, this interface is for custom types only.</remarks>
interface iTypeHandler
{
	/// <summary>True to cache all decoded values in RAM, with int64 offset key</summary>
	/// <remarks>Only set for stuff like continents and countries which come in limited quantities,<br/>
	/// otherwise the cache will consume ridiculous amount of RAM</remarks>
	bool cacheAllValues { get; }

	/// <summary>Resolve serialised property name into field type; return <c>null</c> to skip de-serialising the property.</summary>
	Type? fieldType( in Key key );

	/// <summary>Create object from the hash map with de-serialised properties</summary>
	object create( IReadOnlyDictionary<Key, object> fields );
}