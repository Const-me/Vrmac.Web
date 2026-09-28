using System.Numerics;
namespace MaxMind.Db;

/// <summary>Given a stream, this class decodes the object graph at a particular location</summary>
/// <remarks>Unlike the <c>MaxMind.Db.Decoder</c> class from the OG library, this version is compatible with the AOT trimmer</remarks>
internal sealed class DecoderAot
{
	readonly MemoryMapBuffer _database;
	readonly long _pointerBase;
	readonly bool _followPointers;
	readonly int[] _pointerValueOffset = [ 0, 0, 1 << 11, ( 1 << 19 ) + ( 1 << 11 ), 0 ];

	/// <summary>Initializes a new instance of the <see cref="Decoder" /> class.</summary>
	/// <param name="database">The database.</param>
	/// <param name="pointerBase">The base address in the stream.</param>
	/// <param name="followPointers">Whether to follow pointers. For testing.</param>
	internal DecoderAot( MemoryMapBuffer database, long pointerBase, bool followPointers = true )
	{
		_pointerBase = pointerBase;
		_database = database;
		_followPointers = followPointers;
		typeHandlers = new();
	}

	readonly Dictionary<Type, iTypeHandler> typeHandlers;
	readonly struct CachedEntry
	{
		public readonly object val;
		public readonly long outOffset;
		public CachedEntry( object val, long outOffset )
		{
			this.val = val;
			this.outOffset = outOffset;
		}
	}
	readonly Dictionary<Type, Dictionary<long, CachedEntry>> cachedValues = new();

	/// <summary>Register decoding handler for the type</summary>
	public bool addTypeHandler( Type result, iTypeHandler factory ) =>
		typeHandlers.TryAdd( result, factory );

	/// <summary>Decodes the object at the specified offset.</summary>
	/// <param name="offset">The offset.</param>
	/// <param name="outOffset">The out offset</param>
	/// <returns>An object containing the data read from the stream</returns>
	internal T Decode<T>( long offset, out long outOffset ) where T : class
	{
		if( Decode( typeof( T ), offset, out outOffset ) is not T decoded )
		{
			throw new InvalidDatabaseException( "The value cannot be decoded as " + typeof( T ) );
		}
		return decoded;
	}

	object Decode( Type expectedType, long offset, out long outOffset )
	{
		ObjectType type = CtrlData( offset, out var size, out offset );
		return DecodeByType( expectedType, type, offset, size, out outOffset );
	}

	ObjectType CtrlData( long offset, out int size, out long outOffset )
	{
		if( offset >= _database.Length )
			throw new InvalidDatabaseException( "The MaxMind DB file's data section contains bad data: pointer larger than the database." );

		byte ctrlByte = _database.ReadOne( offset );
		offset++;

		ObjectType type = (ObjectType)( ctrlByte >> 5 );

		if( type == ObjectType.Extended )
		{
			int nextByte = _database.ReadOne( offset );
			int typeNum = nextByte + 7;
			if( typeNum < 8 )
			{
				throw new InvalidDatabaseException( "Something went horribly wrong in the decoder. An extended type "
					+ "resolved to a type number < 8 (" + typeNum + ")" );
			}
			type = (ObjectType)typeNum;
			offset++;
		}

		// The size calculation is inlined as it is hot code
		size = ctrlByte & 0x1f;
		if( size >= 29 )
		{
			int bytesToRead = size - 28;
			size = size switch
			{
				29 => 29 + _database.ReadOne( offset ),
				30 => 285 + _database.ReadVarInt( offset, bytesToRead ),
				_ => 65821 + _database.ReadVarInt( offset, bytesToRead ),
			};
			offset += bytesToRead;
		}
		outOffset = offset;
		return type;
	}

	/// <summary>Decodes the value by type.</summary>
	/// <param name="expectedType"></param>
	/// <param name="type">The type.</param>
	/// <param name="offset">The offset.</param>
	/// <param name="size">The size.</param>
	/// <param name="outOffset">The out offset</param>
	/// <returns></returns>
	/// <exception cref="Exception">Unable to handle type!</exception>
	object DecodeByType( Type expectedType, ObjectType type, long offset, int size, out long outOffset )
	{
		outOffset = offset + size;

		switch( type )
		{
			case ObjectType.Pointer:
				long pointer = DecodePointer( offset, size, out offset );
				outOffset = offset;
				if( !_followPointers )
				{
					return pointer;
				}

				object result = Decode( expectedType, pointer, out _ );
				return result;

			case ObjectType.Map:
				return DecodeMap( expectedType, offset, size, out outOffset );

			case ObjectType.Array:
				return DecodeArray( expectedType, size, offset, out outOffset );

			case ObjectType.Boolean:
				outOffset = offset;
				return DecodeBoolean( expectedType, size );

			case ObjectType.Utf8String:
				return DecodeString( expectedType, offset, size );

			case ObjectType.Double:
				return DecodeDouble( expectedType, offset, size );

			case ObjectType.Float:
				return DecodeFloat( expectedType, offset, size );

			case ObjectType.Bytes:
				return DecodeBytes( expectedType, offset, size );

			case ObjectType.Uint16:
				return DecodeInteger( expectedType, offset, size );

			case ObjectType.Uint32:
				return DecodeLong( expectedType, offset, size );

			case ObjectType.Int32:
				return DecodeInteger( expectedType, offset, size );

			case ObjectType.Uint64:
				return DecodeUInt64( expectedType, offset, size );

			case ObjectType.Uint128:
				return DecodeBigInteger( expectedType, offset, size );

			default:
				throw new InvalidDatabaseException( "Unable to handle type: " + type );
		}
	}

	/// <summary>Decodes the boolean.</summary>
	/// <param name="expectedType"></param>
	/// <param name="size">The size of the structure.</param>
	/// <returns></returns>
	static bool DecodeBoolean( Type expectedType, int size )
	{
		if( expectedType != typeof( bool ) && expectedType != typeof( bool? ) )
			ReflectionUtil.CheckType( expectedType, typeof( bool ) );

		return size switch
		{
			0 => false,
			1 => true,
			_ => throw new InvalidDatabaseException( "The MaxMind DB file's data section contains bad data: invalid size of boolean." ),
		};
	}

	/// <summary>Decodes the double</summary>
	double DecodeDouble( Type expectedType, long offset, int size )
	{
		if( expectedType != typeof( double ) && expectedType != typeof( double? ) )
			ReflectionUtil.CheckType( expectedType, typeof( double ) );
		if( size != 8 )
			throw new InvalidDatabaseException( "The MaxMind DB file's data section contains bad data: invalid size of double." );
		return _database.ReadDouble( offset );
	}

	/// <summary>Decodes the float</summary>
	float DecodeFloat( Type expectedType, long offset, int size )
	{
		if( expectedType != typeof( float ) && expectedType != typeof( float? ) )
			ReflectionUtil.CheckType( expectedType, typeof( float ) );
		if( size != 4 )
			throw new InvalidDatabaseException( "The MaxMind DB file's data section contains bad data: invalid size of float." );
		return _database.ReadFloat( offset );
	}

	/// <summary>Decodes the string</summary>
	string DecodeString( Type expectedType, long offset, int size )
	{
		ReflectionUtil.CheckType( expectedType, typeof( string ) );
		return _database.ReadString( offset, size );
	}

	byte[] DecodeBytes( Type expectedType, long offset, int size )
	{
		ReflectionUtil.CheckType( expectedType, typeof( byte[] ) );
		return _database.Read( offset, size );
	}

	/// <summary>Decodes the map.</summary>
	/// <param name="expectedType"></param>
	/// <param name="offset">The offset.</param>
	/// <param name="size">The size.</param>
	/// <param name="outOffset">The out offset.</param>
	/// <returns></returns>
	object DecodeMap( Type expectedType, long offset, int size, out long outOffset )
	{
		if( !typeHandlers.TryGetValue( expectedType, out iTypeHandler? factory ) )
			throw new DeserializationException( $"The type {expectedType.FullName} doesn't have type handler registered" );

		Dictionary<long, CachedEntry>? cache = null;
		long objectOffset = offset;
		if( factory.cacheAllValues )
		{
			ref Dictionary<long, CachedEntry>? cacheDict = ref CollectionsMarshal.GetValueRefOrAddDefault( cachedValues, expectedType, out bool wasThere );
			if( wasThere )
			{
				cache = cacheDict;
				if( cache!.TryGetValue( offset, out CachedEntry ce ) )
				{
					outOffset = ce.outOffset;
					return ce.val;
				}
			}
			else
			{
				cache = new();
				cacheDict = cache;
			}
		}

		// TODO [low, performance]: cache in [ThreadStatic]
		// The problem is this method called recursively, need a whole stack of temporary dictionaries.
		// Let's hope .NET 10 AOT is fast enough collecting from Gen0
		Dictionary<Key, object> dict = new();

		for( int i = 0; i < size; i++ )
		{
			Key key = DecodeKey( offset, out offset );
			Type? ft = factory.fieldType( key );
			if( null != ft )
				dict.Add( key, Decode( ft, offset, out offset ) );
			else
				offset = NextValueOffset( offset, 1 );
		}

		object result = factory.create( dict );
		cache?.Add( objectOffset, new CachedEntry( result, offset ) );
		outOffset = offset;
		return result;
	}

	Key DecodeKey( long offset, out long outOffset )
	{
		ObjectType type = CtrlData( offset, out var size, out offset );
		switch( type )
		{
			case ObjectType.Pointer:
				offset = DecodePointer( offset, size, out outOffset );
				return DecodeKey( offset, out _ );

			case ObjectType.Utf8String:
				outOffset = offset + size;
				return new Key( _database, offset, size );

			default:
				throw new InvalidDatabaseException( $"Database contains a non-string as map key: {type}" );
		}
	}

	long NextValueOffset( long offset, int numberToSkip )
	{
		while( true )
		{
			if( numberToSkip == 0 )
				return offset;

			ObjectType type = CtrlData( offset, out var size, out offset );
			switch( type )
			{
				case ObjectType.Pointer:
					// While skipping values, only pointer byte-length matters.
					offset += ( ( size >> 3 ) & 0x3 ) + 1;
					break;

				case ObjectType.Map:
					numberToSkip += 2 * size;
					break;

				case ObjectType.Array:
					numberToSkip += size;
					break;

				case ObjectType.Boolean:
					break;

				default:
					offset += size;
					break;
			}
			numberToSkip--;
		}
	}

	/// <summary>Decodes the long</summary>
	long DecodeLong( Type expectedType, long offset, int size )
	{
		if( expectedType != typeof( long ) && expectedType != typeof( long? ) )
			ReflectionUtil.CheckType( expectedType, typeof( long ) );
		return _database.ReadLong( offset, size );
	}

	/// <summary>Decodes the array.</summary>
	/// <param name="expectedType"></param>
	/// <param name="size">The size.</param>
	/// <param name="offset">The offset.</param>
	/// <param name="outOffset">The out offset.</param>
	/// <returns></returns>
	object DecodeArray( Type expectedType, int size, long offset, out long outOffset )
	{
		object array;

		// Fast path for List<string> (and parents).
		if( expectedType != typeof( object ) && expectedType.IsAssignableFrom( typeof( List<string> ) ) )
		{
			List<string> list = new( size );
			for( int i = 0; i < size; i++ )
			{
				var r = Decode<string>( offset, out offset );
				list.Add( r );
			}
			array = list;
		}
		else
		{
			/*
			Type[] genericArgs = expectedType.GetGenericArguments();
			Type? argType = genericArgs.Length == 0 ? typeof( object ) : genericArgs[ 0 ];
			Type interfaceType = typeof( ICollection<> ).MakeGenericType( argType );
			if( interfaceType == null )
				throw new DeserializationException( "Unexpected null generic type while decoding array" );

			MethodInfo? addMethod = interfaceType.GetMethod( "Add" );
			if( addMethod == null )
				throw new DeserializationException( "Missing Add method when decoding array" );

			array = _listActivatorCreator.GetActivator( expectedType )( size );
			for( int i = 0; i < size; i++ )
			{
				object r = Decode( argType, offset, out offset, injectables, network );
				addMethod.Invoke( array, [ r ] );
			} */
			throw new NotImplementedException( "Arrays of objects are not implemented yet" );
		}

		outOffset = offset;
		return array;
	}

	/// <summary>Decodes the uint64</summary>
	ulong DecodeUInt64( Type expectedType, long offset, int size )
	{
		if( expectedType != typeof( ulong ) && expectedType != typeof( ulong? ) )
			ReflectionUtil.CheckType( expectedType, typeof( ulong ) );
		return _database.ReadULong( offset, size );
	}

	/// <summary>Decodes the big integer</summary>
	BigInteger DecodeBigInteger( Type expectedType, long offset, int size )
	{
		if( expectedType != typeof( BigInteger ) && expectedType != typeof( BigInteger? ) )
			ReflectionUtil.CheckType( expectedType, typeof( BigInteger ) );
		return _database.ReadBigInteger( offset, size );
	}

	/// <summary>Decodes the pointer.</summary>
	/// <param name="offset">The offset.</param>
	/// <param name="size"></param>
	/// <param name="outOffset">The resulting offset</param>
	/// <returns></returns>
	long DecodePointer( long offset, int size, out long outOffset )
	{
		int pointerSize = ( ( size >> 3 ) & 0x3 ) + 1;
		int b = pointerSize == 4 ? 0 : size & 0x7;
		// Cast through uint so that 4-byte values >= 2^31 are
		// zero-extended to long rather than sign-extended.
		long packed = ( (long)b << ( 8 * pointerSize ) ) | (long)(uint)_database.ReadVarInt( offset, pointerSize );
		outOffset = offset + pointerSize;
		return packed + _pointerBase + _pointerValueOffset[ pointerSize ];
	}

	/// <summary>Decodes the integer</summary>
	int DecodeInteger( Type expectedType, long offset, int size )
	{
		if( expectedType != typeof( int ) && expectedType != typeof( int? ) )
			ReflectionUtil.CheckType( expectedType, typeof( int ) );
		return _database.ReadVarInt( offset, size );
	}
}