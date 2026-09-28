// Adapted from MaxMind-DB-Reader-dotnet 5.0, slightly modified on 2026-05-13
using System.Text;
namespace MaxMind.Db;

[StructLayout( LayoutKind.Auto )]
internal readonly struct Key: IEquatable<Key>
{
	bool isBufferBacked => null == _bytes;
	readonly MemoryMapBuffer? _buffer;
	readonly byte[]? _bytes;
	readonly long _offset;
	readonly int _size;
	readonly int _hashCode;

	/// <summary>Create from the slice of memory mapped storage</summary>
	public Key( MemoryMapBuffer buffer, long offset, int size )
	{
		_buffer = buffer;
		_bytes = null;
		_offset = offset;
		_size = size;
		_hashCode = buffer.HashBytes( offset, size );
	}

	/// <summary>Create from array of bytes alreaedy in the GC heap</summary>
	public Key( byte[] bytes )
	{
		_buffer = null;
		_bytes = bytes;
		_offset = 0;
		_size = bytes.Length;
		_hashCode = HashBytes( bytes );
	}

	/// <summary>Create with the copy of the span to the GC heap</summary>
	public Key( ReadOnlySpan<byte> bytes )
	{
		_buffer = null;
		_bytes = bytes.ToArray();
		_offset = 0;
		_size = bytes.Length;
		_hashCode = HashBytes( _bytes );
	}

	public bool Equals( Key other )
	{
		if( _size != other._size )
			return false;

		if( isBufferBacked )
		{
			var buffer = _buffer!;
			if( other.isBufferBacked )
				return buffer.EqualsBytes( _offset, other._buffer!, other._offset, _size );
			return other._bytes != null && buffer.EqualsBytes( _offset, other._bytes, 0, _size );
		}

		byte[]? bytes = _bytes;
		if( bytes == null )
			return !other.isBufferBacked && other._bytes == null;

		if( other.isBufferBacked )
			return other._buffer!.EqualsBytes( other._offset, bytes, 0, _size );
		return other._bytes != null && bytes.AsSpan( 0, _size ).SequenceEqual( other._bytes.AsSpan( 0, _size ) );
	}

	public override bool Equals( object? obj ) => obj is Key other && Equals( other );

	public static bool operator ==( in Key a, in Key b ) => a.Equals( b );
	public static bool operator !=( in Key a, in Key b ) => !a.Equals( b );

	public override int GetHashCode() => _hashCode;

	static int HashBytes( byte[] bytes )
	{
		var code = 17;
		for( var i = 0; i < bytes.Length; i++ )
			code = unchecked(( 31 * code ) + bytes[ i ]);
		return code;
	}

	/// <summary>Assuming the key is UTF-8 string, convert bytes to string</summary>
	public override string ToString()
	{
		ReadOnlySpan<byte> span;
		if( isBufferBacked )
			span = _buffer!.GetSpan( _offset, _size );
		else
			span = _bytes!.AsSpan();
		return Encoding.UTF8.GetString( span );
	}
}