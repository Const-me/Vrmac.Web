namespace Vrmac.Mapper;

/// <summary>Scalar value returned from a query</summary>
public readonly struct ScalarValue
{
	readonly object? obj;
	internal ScalarValue( object? obj )
	{
		if( obj is DBNull )
			obj = null;
		this.obj = obj;
	}
	bool notNull => null != obj;

	/// <summary>Convert to <c>int</c></summary>
	public int? int32 => notNull ? Convert.ToInt32( obj ) : null;
	/// <summary>Convert to <c>uint</c></summary>
	public uint? uint32 => notNull ? Convert.ToUInt32( obj ) : null;
	/// <summary>Convert to <c>ulong</c>, coalescing null into zero</summary>
	public ulong uint64 => notNull ? Convert.ToUInt64( obj ) : 0;
	/// <summary>Convert to <c>long</c>, coalescing null into zero</summary>
	public long int64 => notNull ? Convert.ToInt64( obj ) : 0;
	/// <summary>Convert to array of bytes</summary>
	public byte[]? bytes => obj as byte[];
	/// <summary>Convert to bool, coalescing null into false</summary>
	public bool boolean => notNull ? ( Convert.ToInt32( obj ) != 0 ) : false;
	/// <summary>Convert to string</summary>
	public string? str => obj as string;
}