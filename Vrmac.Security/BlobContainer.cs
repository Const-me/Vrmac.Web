using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Vrmac.Security;

/// <summary>Utility class to pack multiple byte blobs into a single container</summary>
public static class BlobContainer
{
	/// <summary>Parse container into blobs</summary>
	/// <remarks>The function doesn't copy the bytes, it returns blobs sliced from the input container</remarks>
	public static ReadOnlyMemory<byte>[] unpack( ReadOnlyMemory<byte> container, uint packageSignature, int maxBlobs = 1024 )
	{
		// Verify the signature
		ReadOnlySpan<byte> span = container.Span;
		uint signature = MemoryMarshal.Read<uint>( span );
		if( signature != packageSignature )
			throw new ArgumentException( "Unexpected container signature" );
		span = span.Slice( 4 );

		// Parse all these variable length integers
		int blobs, cb;
		blobs = MultiByte.parse( span, out cb );
		span = span.Slice( cb );

		const string failPadding = "Unexpected padding in the blob container";
		if( 0 == blobs )
		{
			if( span.IsEmpty )
				return Array.Empty<ReadOnlyMemory<byte>>();
			throw new ArgumentException( failPadding );
		}
		if( blobs > maxBlobs )
			throw new ArgumentException( "The container has too many blobs" );

		int cbTotal = 0;
		Span<int> lengths = ( blobs <= 256 ) ? stackalloc int[ blobs ] : new int[ blobs ];
		for( int i = 0; i < blobs; i++ )
		{
			int len = MultiByte.parse( span, out cb );
			span = span.Slice( cb );
			lengths[ i ] = len;
			cbTotal += len;
		}

		if( span.Length != cbTotal )
		{
			string message = ( span.Length < cbTotal ) ? "The blob container is truncated or corrupted" : failPadding;
			throw new ArgumentException( message );
		}

		// Slice what's left in the container into individual blobs
		container = container.Slice( container.Length - span.Length );
		ReadOnlyMemory<byte>[] result = new ReadOnlyMemory<byte>[ blobs ];
		for( int i = 0; i < blobs; i++ )
		{
			cb = lengths[ i ];
			result[ i ] = container.Slice( 0, cb );
			container = container.Slice( cb );
		}

		Debug.Assert( container.IsEmpty );
		return result;
	}

	/// <summary>Pack blobs into the container</summary>
	public static byte[] pack( uint packageSignature, ReadOnlyMemory<byte>[] blobs )
	{
		// Reserve list capacity assuming the count is below 16k, and each blob is below 2 MB
		List<byte> list = new( 2 + blobs.Length * 3 );
		// Encode count of blobs
		MultiByte.encode( list, blobs.Length );
		int cbTotal = 0;
		// Encode length of each blob
		foreach( ReadOnlyMemory<byte> mem in blobs )
		{
			int cb = mem.Length;
			MultiByte.encode( list, cb );
			cbTotal += cb;
		}

		byte[] result = new byte[ 4 + list.Count + cbTotal ];
		try
		{
			Span<byte> span = result;
			// Write the signature
			MemoryMarshal.Write( span, packageSignature );
			span = span.Slice( 4 );

			// Copy variable-length header from the list into the output array
			ReadOnlySpan<byte> headerSpan = CollectionsMarshal.AsSpan( list );
			headerSpan.CopyTo( span );
			span = span.Slice( headerSpan.Length );

			// Append the payload data
			foreach( ReadOnlyMemory<byte> mem in blobs )
			{
				ReadOnlySpan<byte> blobSpan = mem.Span;
				blobSpan.CopyTo( span );
				span = span.Slice( blobSpan.Length );
			}

			Debug.Assert( span.IsEmpty );
			return result;
		}
		catch
		{
			CryptographicOperations.ZeroMemory( result );
			throw;
		}
	}
}