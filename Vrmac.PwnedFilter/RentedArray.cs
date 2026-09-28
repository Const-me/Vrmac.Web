using System.Buffers;
namespace Vrmac.PwnedFilter;

struct RentedArray: IDisposable
{
	byte[]? arr;
	readonly int length;

	public RentedArray( ReadOnlySpan<byte> bytes )
	{
		arr = ArrayPool<byte>.Shared.Rent( bytes.Length );
		length = bytes.Length;
		bytes.CopyTo( arr.AsSpan( 0, length ) );
	}

	public ReadOnlyMemory<byte> memory => arr.AsMemory( 0, length );

	public void Dispose()
	{
		if( null != arr )
		{
			ArrayPool<byte>.Shared.Return( arr );
			arr = null;
		}
	}
}