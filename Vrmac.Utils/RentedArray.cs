using System.Buffers;
using System.Net;
using System.Runtime.InteropServices;
namespace Vrmac;

/// <summary>HTTP status code of a response,<br/>
/// and an optional array of bytes rented from the pool with response body</summary>
[StructLayout( LayoutKind.Auto )]
public struct RentedArray: IDisposable
{
	byte[]? arr;
	/// <summary>Length of the response body; might be zero</summary>
	public readonly int length;
	/// <summary>HTTP status code as per RFC 2616</summary>
	public readonly HttpStatusCode statusCode;
	/// <summary>True when the status code is HTTP 200 OK</summary>
	public bool success => statusCode == HttpStatusCode.OK;
	/// <summary>The content of the response body; might be empty</summary>
	public ReadOnlyMemory<byte> memory => arr.AsMemory( 0, length );

	/// <summary>Span to write response body; might be empty</summary>
	public Span<byte> span => arr.AsSpan( 0, length );

	/// <summary>Copy bytes from memory stream into rented array, and set HTTP status 200 OK</summary>
	public RentedArray( MemoryStream ms )
	{
		length = (int)ms.Length;
		arr = ArrayPool<byte>.Shared.Rent( length );
		try
		{
			ms.Seek( 0, SeekOrigin.Begin );
			ms.ReadExactly( arr.AsSpan( 0, length ) );
			statusCode = HttpStatusCode.OK;
		}
		catch
		{
			ArrayPool<byte>.Shared.Return( arr );
			throw;
		}
	}

	/// <summary>Create a zero-length response</summary>
	public RentedArray( HttpStatusCode statusCode )
	{
		Debug.Assert( statusCode != HttpStatusCode.OK );
		this.statusCode = statusCode;
	}

	/// <summary>Rent array from the pool</summary>
	public static RentedArray create( int length, HttpStatusCode statusCode = HttpStatusCode.OK )
	{
		byte[] arr = ArrayPool<byte>.Shared.Rent( length );
		return new RentedArray( arr, length, statusCode );
	}

	/// <summary>Release rented array back to the pool</summary>
	public void Dispose()
	{
		byte[]? arr = Interlocked.Exchange( ref this.arr, null );
		if( null != arr )
			ArrayPool<byte>.Shared.Return( arr );
	}

	RentedArray( byte[] arr, int length, HttpStatusCode statusCode = HttpStatusCode.OK )
	{
		this.arr = arr;
		this.length = length;
		this.statusCode = statusCode;
	}

	/// <summary>Load file from disk into rented array of bytes</summary>
	public static RentedArray readAllBytes( string path )
	{
		if( !File.Exists( path ) )
			return new RentedArray( HttpStatusCode.NotFound );
		byte[]? arr = null;
		try
		{
			using FileStream stream = File.OpenRead( path );
			int length = (int)stream.Length;
			arr = ArrayPool<byte>.Shared.Rent( length );
			stream.ReadExactly( arr.AsSpan( 0, length ) );
			return new RentedArray( arr, length );
		}
		catch
		{
			if( null != arr )
				ArrayPool<byte>.Shared.Return( arr );
			return new RentedArray( HttpStatusCode.InternalServerError );
		}
	}

	/// <summary>Split bytes into lines</summary>
	public IEnumerable<ReadOnlyMemory<byte>> parseLines()
	{
		ReadOnlyMemory<byte> mem = arr.AsMemory( 0, length );
		return LineParser.parse( mem );
	}
}