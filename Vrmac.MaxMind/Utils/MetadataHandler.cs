namespace MaxMind.Db;

/// <summary>Type handler to decode database metadata</summary>
/// <seealso cref="Metadata" />
sealed class MetadataHandler: iTypeHandler
{
	public static void register( DecoderAot decoder )
	{
		decoder.addTypeHandler( typeof( Metadata ), instance );
		StringMapHandler.register( decoder );
	}

	Type? iTypeHandler.fieldType( in Key key ) => fieldTypes.GetValueOrDefault( key );

	object iTypeHandler.create( IReadOnlyDictionary<Key, object> fields )
	{
		int ver1 = (int)fields[ verMajor ];
		int ver2 = (int)fields[ verMinor ];
		ulong build = (ulong)fields[ buildEpoch ];
		string dt = (string)fields[ databaseType ];
		Dictionary<string, string> ds = (Dictionary<string, string>)fields[ desc ];
		int ip = (int)fields[ ipVersion ];
		List<string> lan = (List<string>)fields[ langs ];
		long nodes = (long)fields[ nodeCount ];
		int rec = (int)fields[ recordSize ];
		return new Metadata( ver1, ver2, build, dt, ds, ip, lan, nodes, rec );
	}
	bool iTypeHandler.cacheAllValues => false;

	readonly Dictionary<Key, Type> fieldTypes;

	readonly Key verMajor = new Key( "binary_format_major_version"u8 );
	readonly Key verMinor = new Key( "binary_format_minor_version"u8 );
	readonly Key buildEpoch = new Key( "build_epoch"u8 );
	readonly Key databaseType = new Key( "database_type"u8 );
	readonly Key desc = new Key( "description"u8 );
	readonly Key ipVersion = new Key( "ip_version"u8 );
	readonly Key nodeCount = new Key( "node_count"u8 );
	readonly Key recordSize = new Key( "record_size"u8 );
	readonly Key langs = new Key( "languages"u8 );

	private MetadataHandler()
	{
		fieldTypes = new( 7 )
		{
			{ verMajor, typeof(int) },
			{ verMinor, typeof(int) },
			{ buildEpoch, typeof(ulong) },
			{ databaseType, typeof(string) },
			{ desc, typeof(Dictionary<string,string>) },
			{ ipVersion, typeof(int) },
			{ langs, typeof(List<string>) },
			{ nodeCount, typeof(long) },
			{ recordSize, typeof(int) },
		};
	}

	static readonly iTypeHandler instance = new MetadataHandler();
}