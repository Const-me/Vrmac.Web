using System.Runtime.CompilerServices;
namespace Vrmac.Html;

/// <summary>Thread-local MemoryStream created on first use</summary>
public static class CachedMemStream
{
	/// <summary>Copy response into array of bytes rented from <c>ArrayPool&lt;byte&gt;.Shared</c></summary>
	/// <remarks>This is to enable concurrent async socket writes</remarks>
	public static RentedArray copyBytes( this MemoryStream memStream ) => new RentedArray( memStream );

	[ThreadStatic]
	static MemoryStream? ts_buffer;

	/// <summary>Get empty MemoryStream cached in a thread static field</summary>
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	public static MemoryStream threadLocal()
	{
		MemoryStream? memStream = ts_buffer;
		if( memStream != null )
		{
			memStream.SetLength( 0 );
			return memStream;
		}

		return create();
	}

	[MethodImpl( MethodImplOptions.NoInlining )]
	static MemoryStream create()
	{
		MemoryStream memStream = new();
		ts_buffer = memStream;
		return memStream;
	}
}