// Adapted from MaxMind-DB-Reader-dotnet 5.0 modifying substantially; last change on 2026-05-16
using System.Net.Sockets;
using Vrmac.MaxMind;
using Vrmac.MaxMind.Reshape;
namespace MaxMind.Db;

/// <summary>An enumeration specifying the API to use to read the database</summary>
public enum FileAccessMode
{
	/// <summary>Open the file in memory mapped mode. Does not load into real memory.</summary>
	MemoryMapped,

	/// <summary>Open the file in global memory mapped mode. Requires the 'create global objects' right. Does not load into real memory.</summary>
	/// <remarks>For information on the 'create global objects' right, see: https://docs.microsoft.com/en-us/windows/security/threat-protection/security-policy-settings/create-global-objects</remarks>
	MemoryMappedGlobal,

	/// <summary>Read the file into an anonymous memory-mapped region that is private to this process.
	/// Requires that the platform supports memory-mapped files.</summary>
	Memory,
}

/// <summary>Given a MaxMind DB file, this class will retrieve information about an IP address</summary>
/// <remarks>Unlike the OG impementation, this version <b>is not</b> thread safe</remarks>
sealed class Reader: IDisposable
{
	/// <summary>A node from the reader iterator</summary>
	public struct ReaderIteratorNode<T>
	{
		/// <summary>Internal constructor</summary>
		/// <param name="start">Start ip</param>
		/// <param name="prefixLength">Prefix length</param>
		/// <param name="data">Data</param>
		internal ReaderIteratorNode( IPAddress start, int prefixLength, T data )
		{
			Start = start;
			PrefixLength = prefixLength;
			Data = data;
		}

		/// <summary>Start ip address</summary>
		public IPAddress Start { get; }

		/// <summary>Prefix/mask length</summary>
		public int PrefixLength { get; }

		/// <summary>Data</summary>
		public T Data { get; }
	}

	struct NetNode
	{
		public byte[] IPBytes { get; set; }
		public int Bit { get; set; }
		public long Pointer { get; set; }
	}

	const int DataSectionSeparatorSize = 16;

	// IPv4 addresses are stored 96 bits deep in an IPv6 search tree
	// (128 - 32 = 96). The reader pre-walks these nodes at construction
	// time so IPv4 lookups can skip directly to the relevant subtree.
	const int IPv4PrefixInIPv6Tree = 96;
	readonly MemoryMapBuffer _database;
	readonly string? _fileName;
	readonly long _dataPointerOffset;
	readonly int _dbIPVersion;
	readonly long _nodeByteSize;
	readonly long _nodeCount;
	readonly int _recordSize;
	readonly long searchTreeSize;

	static readonly byte[] _metadataStartMarker = [ 0xAB, 0xCD, 0xEF, 77, 97, 120, 77, 105, 110, 100, 46, 99, 111, 109 ];

	bool _disposed;
	readonly long _ipV4Start;

	/// <summary>Initializes a new instance of the <see cref="Reader" /> class.</summary>
	/// <param name="file">The file.</param>
	public Reader( string file ) : this( file, FileAccessMode.MemoryMapped )
	{
	}

	/// <summary>Initializes a new instance of the <see cref="Reader" /> class.</summary>
	/// <param name="file">The MaxMind DB file.</param>
	/// <param name="mode">The mode by which to access the DB file.</param>
	public Reader( string file, FileAccessMode mode ) : this( BufferForMode( file, mode ), file )
	{
	}

	/// <summary>Initialize with <c>Stream</c>. The current position of the stream must point to the start of the database.
	/// The content between the current position and the end of the stream must be a valid MaxMind DB.</summary>
	/// <param name="stream">The stream to use. It will be used from its current position.</param>
	/// <exception cref="ArgumentNullException"></exception>
	public Reader( Stream stream ) : this( new MemoryMapBuffer( stream ), null )
	{
	}

	Reader( MemoryMapBuffer buffer, string? file )
	{
		_fileName = file;
		_database = buffer;
		var start = FindMetadataStart();
		var metaDecode = new DecoderAot( _database, start );
		MetadataHandler.register( metaDecode );
		Metadata = metaDecode.Decode<Metadata>( start, out _ );
		_dataPointerOffset = Metadata.SearchTreeSize - Metadata.NodeCount;
		_dbIPVersion = Metadata.IPVersion;
		_nodeByteSize = Metadata.NodeByteSize;
		_nodeCount = Metadata.NodeCount;
		_recordSize = Metadata.RecordSize;
		searchTreeSize = Metadata.SearchTreeSize;
		Decoder = new DecoderAot( _database, Metadata.SearchTreeSize + DataSectionSeparatorSize );
		RecordHandler.register( Decoder );

		if( _dbIPVersion == 6 )
		{
			long node = 0;
			for( var i = 0; i < IPv4PrefixInIPv6Tree && node < _nodeCount; i++ )
			{
				node = ReadNode( node, 0 );
			}
			_ipV4Start = node;
		}
	}

	/// <summary>Asynchronously initializes a new instance of the <see cref="Reader" /> class by reading the specified file into a memory-mapped region.</summary>
	/// <param name="file">The file.</param>
	public static async Task<Reader> CreateAsync( string file ) =>
		new Reader( await MemoryMapBuffer.CreateAsync( file ).ConfigureAwait( false ), file );

	/// <summary>Asynchronously initialize with Stream.</summary>
	/// <param name="stream">The stream to use. It will be used from its current position. </param>
	/// <exception cref="ArgumentNullException"></exception>
	public static async Task<Reader> CreateAsync( Stream stream ) =>
		new Reader( await MemoryMapBuffer.CreateAsync( stream ).ConfigureAwait( false ), null );

	static MemoryMapBuffer BufferForMode( string file, FileAccessMode mode )
	{
		return mode switch
		{
			FileAccessMode.MemoryMapped => new MemoryMapBuffer( file, false ),
			FileAccessMode.MemoryMappedGlobal => new MemoryMapBuffer( file, true ),
			FileAccessMode.Memory => new MemoryMapBuffer( file ),
			_ => throw new ArgumentException( "Unknown file access mode" ),
		};
	}

	/// <summary>The metadata for the open database.</summary>
	/// <value>The metadata.</value>
	public Metadata Metadata { get; }

	private DecoderAot Decoder { get; }

	/// <summary>Release resources back to the system.</summary>
	public void Dispose()
	{
		Dispose( true );
		GC.SuppressFinalize( this );
	}

	/// <summary>Release resources back to the system.</summary>
	/// <param name="disposing"></param>
	private void Dispose( bool disposing )
	{
		if( _disposed )
			return;

		if( disposing )
		{
			_database.Dispose();
		}

		_disposed = true;
	}

	/// <summary>Finds the data related to the specified address.</summary>
	/// <param name="ipAddress">The IP address.</param>
	/// <param name="injectables">Value to inject during deserialization</param>
	/// <returns>An object containing the IP related data</returns>
	public T? Find<T>( IPAddress ipAddress ) where T : class
	{
		return Find<T>( ipAddress, out _ );
	}

	/// <summary>Finds the data related to the specified address.</summary>
	/// <param name="ipAddress">The IP address.</param>
	/// <param name="prefixLength">The network prefix length for the network record in the database containing the IP address looked up.</param>
	/// <param name="injectables">Value to inject during deserialization</param>
	/// <returns>An object containing the IP related data</returns>
	public T? Find<T>( IPAddress ipAddress, out int prefixLength ) where T : class
	{
		var pointer = FindAddressInTree( ipAddress, out prefixLength );
		if( pointer == 0 )
			return null;
		return ResolveDataPointer<T>( pointer );
	}

	/// <summary><para>Get an enumerator that iterates all data nodes in the database. Do not modify the object as it may be cached.</para>
	/// <para>Note that due to caching, the Network attribute on constructor parameters will be ignored.</para></summary>
	/// <param name="injectables">Value to inject during deserialization</param>
	/// <param name="cacheSize">The size of the data cache. This can greatly speed enumeration at the cost of memory usage.</param>
	/// <returns>Enumerator for all data nodes</returns>
	public IEnumerable<ReaderIteratorNode<T>> FindAll<T>( int cacheSize = 16384 ) where T : class
	{
		var byteCount = _dbIPVersion == 6 ? 16 : 4;
		var nodes = new List<NetNode>();
		var root = new NetNode { IPBytes = new byte[ byteCount ] };
		nodes.Add( root );
		var dataCache = new CachedDictionary<long, T>( cacheSize, null );
		while( nodes.Count > 0 )
		{
			var node = nodes[ nodes.Count - 1 ];
			nodes.RemoveAt( nodes.Count - 1 );
			while( true )
			{
				if( node.Pointer < _nodeCount )
				{
					var ipRight = new byte[ byteCount ];
					Array.Copy( node.IPBytes, ipRight, ipRight.Length );
					if( ipRight.Length <= node.Bit >> 3 )
						throw new InvalidDatabaseException( "Invalid search tree, bad bit " + node.Bit );
					ipRight[ node.Bit >> 3 ] |= (byte)( 1 << ( 7 - ( node.Bit % 8 ) ) );
					var rightPointer = ReadNode( node.Pointer, 1 );
					node.Bit++;
					nodes.Add( new NetNode { Pointer = rightPointer, IPBytes = ipRight, Bit = node.Bit } );
					node.Pointer = ReadNode( node.Pointer, 0 );
				}
				else
				{
					if( node.Pointer > _nodeCount )
					{
						// data node, we are done with this branch
						if( !dataCache.TryGetValue( node.Pointer, out var data ) )
						{
							data = ResolveDataPointer<T>( node.Pointer );
							dataCache.Add( node.Pointer, data );
						}
						var isIPV4 = true;
						for( var i = 0; i < node.IPBytes.Length - 4; i++ )
						{
							if( node.IPBytes[ i ] == 0 ) continue;

							isIPV4 = false;
							break;
						}
						if( !isIPV4 || node.IPBytes.Length == 4 )
						{
							yield return new ReaderIteratorNode<T>( new IPAddress( node.IPBytes ), node.Bit, data );
						}
						else
						{
							var ipV4Bytes = new byte[ 4 ];
							Array.Copy( node.IPBytes, 12, ipV4Bytes, 0, 4 );
							yield return new ReaderIteratorNode<T>( new IPAddress( ipV4Bytes ), node.Bit - IPv4PrefixInIPv6Tree, data );
						}
					}
					// else node is an empty node (terminator node), we are done with this branch
					break;
				}
			}
		}
	}

	internal Builder exportDatabase()
	{
		if( _dbIPVersion != 6 )
			throw new NotImplementedException( "The exporter is only compatible with database version = 6" );

		Builder builder = new( _recordSize, _nodeCount, _database.GetSpan( 0, (int)searchTreeSize ) );

		int byteCount = _dbIPVersion == 6 ? 16 : 4;
		List<NetNode> nodes = [ new NetNode { IPBytes = new byte[ byteCount ] } ];

		const int cacheSize = 16384;
		CachedDictionary<long, Record> dataCache = new( cacheSize, null );
		while( nodes.Count > 0 )
		{
			NetNode node = nodes[ nodes.Count - 1 ];
			nodes.RemoveAt( nodes.Count - 1 );
			while( true )
			{
				if( node.Pointer < _nodeCount )
				{
					byte[] ipRight = new byte[ byteCount ];
					Array.Copy( node.IPBytes, ipRight, ipRight.Length );
					if( ipRight.Length <= node.Bit >> 3 )
						throw new InvalidDatabaseException( "Invalid search tree, bad bit " + node.Bit );
					ipRight[ node.Bit >> 3 ] |= (byte)( 1 << ( 7 - ( node.Bit % 8 ) ) );

					long rightPointer = ReadNode( node.Pointer, 1 );
					node.Bit++;
					nodes.Add( new NetNode { Pointer = rightPointer, IPBytes = ipRight, Bit = node.Bit } );
					node.Pointer = ReadNode( node.Pointer, 0 );
				}
				else
				{
					if( node.Pointer > _nodeCount )
					{
						// data node, we are done with this branch
						if( !dataCache.TryGetValue( node.Pointer, out var data ) )
						{
							data = ResolveDataPointer<Record>( node.Pointer );
							dataCache.Add( node.Pointer, data );
						}
						builder.add( node.Pointer, data );
					}
					// else node is an empty node (terminator node), we are done with this branch
					break;
				}
			}
		}

		return builder;
	}

	T ResolveDataPointer<T>( long pointer ) where T : class
	{
		long resolved = pointer + _dataPointerOffset;
		if( resolved >= _database.Length )
			throw new InvalidDatabaseException( "The MaxMind Db file's search tree is corrupt: contains pointer larger than the database." );
		return Decoder.Decode<T>( resolved, out _ );
	}

	long FindAddressInTree( IPAddress address, out int prefixLength )
	{
		Span<byte> rawAddress = stackalloc byte[ address.AddressFamily == AddressFamily.InterNetwork ? 4 : 16 ];
		if( address.TryWriteBytes( rawAddress, out int rawAddressLength ) )
			rawAddress = rawAddress[ ..rawAddressLength ];
		else
		{
			// Defensive check.
			rawAddress = address.GetAddressBytes();
		}
		return FindAddressInTree( rawAddress, out prefixLength );
	}

	long FindAddressInTree( ReadOnlySpan<byte> rawAddress, out int prefixLength )
	{
		int bitLength = rawAddress.Length * 8;
		long record = StartNode( bitLength );
		long nodeCount = _nodeCount;

		int i = 0;
		for( ; i < bitLength && record < nodeCount; i++ )
		{
			int bit = 1 & ( rawAddress[ i >> 3 ] >> ( 7 - ( i % 8 ) ) );
			record = ReadNode( record, bit );
		}
		prefixLength = i;
		if( record == nodeCount )
		{
			// record is empty
			return 0;
		}
		if( record > nodeCount )
		{
			// record is a data pointer
			return record;
		}
		throw new InvalidDatabaseException( "The MaxMind DB search tree is corrupt: a record value pointed back into the search tree." );
	}

	private long StartNode( int bitLength )
	{
		// Check if we are looking up an IPv4 address in an IPv6 tree. If this
		// is the case, we can skip over the first IPv4PrefixInIPv6Tree nodes.
		if( _dbIPVersion == 6 && bitLength == 32 )
			return _ipV4Start;
		// The first node of the tree is always node 0, at the beginning of the
		// value
		return 0;
	}

	long FindMetadataStart()
	{
		var dbLength = _database.Length;
		var markerLength = _metadataStartMarker.Length;

		for( var i = dbLength - markerLength; i > 0; i-- )
		{
			if( _database.EqualsBytes( i, _metadataStartMarker, 0, markerLength ) )
				return i + markerLength;
		}

		throw new InvalidDatabaseException( $"Could not find a MaxMind Db metadata marker in this file ({_fileName}). Is this a valid MaxMind Db file?" );
	}

	long ReadNode( long nodeNumber, int index )
	{
		var baseOffset = nodeNumber * _nodeByteSize;
		var size = _recordSize;
		switch( size )
		{
			case 24:
				{
					var offset = baseOffset + ( index * 3 );
					return _database.ReadVarInt( offset, 3 );
				}
			case 28:
				{
					if( index == 0 )
					{
						var v = _database.ReadInt( baseOffset );
						return ( v & 0xF0 ) << 20 | ( 0xFFFFFF & ( v >> 8 ) );
					}
					return _database.ReadInt( baseOffset + 3 ) & 0x0FFFFFFF;
				}
			case 32:
				{
					var offset = baseOffset + ( index * 4 );
					// Cast through uint so the sign bit is treated as a
					// value bit. The implicit uint -> long widening then
					// preserves the unsigned value. We use ReadInt rather
					// than ReadLong because ReadInt reads all 4 bytes in
					// one operation.
					return (uint)_database.ReadInt( offset );
				}
		}

		throw new InvalidDatabaseException( $"Unknown record size: {size}" );
	}
}