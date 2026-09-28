using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace Vrmac.PwnedFilter;

/// <summary>Utility functions to consume source dataset from the incrementally updated directory</summary>
static class MultiFiles
{
	/// <summary>Parse sha1.index into sequence of file names</summary>
	public static IEnumerable<string> listFiles( string folder )
	{
		string pathIndex = Path.Combine( folder, "sha1.index" );
		if( !File.Exists( pathIndex ) )
			throw new FileNotFoundException( "The index file is missing; expected there: " + pathIndex );

		byte[] bytes = File.ReadAllBytes( pathIndex );
		List<byte> list = new List<byte>( 16 );
		Encoding enc = Encoding.ASCII;
		foreach( ReadOnlyMemory<byte> mem in LineParser.parse( bytes ) )
		{
			ReadOnlySpan<byte> line = mem.Span;
			line = line.Trim( "\r\n\t "u8 );
			if( line.IsEmpty )
				continue;
			int idx = line.IndexOf( (byte)'\t' );
			if( idx <= 0 )
				throw new ArgumentException( "Unexpected line in the sha1.index" );

			list.Clear();
			list.AddRange( line.Slice( 0, idx ) );
			list.AddRange( ".txt"u8 );

			string fileName = enc.GetString( CollectionsMarshal.AsSpan( list ) );
			Debug.Assert( File.Exists( Path.Combine( folder, fileName ) ) );
			yield return fileName;
		}
	}

	/// <summary>Load the entire text file to RAM, parse into sequence of lines</summary>
	/// <remarks>Should only be called for incrementally downloaded datasets; the single file is way too large to be loaded into RAM</remarks>
	public static IEnumerable<ReadOnlyMemory<byte>> parseLines( string path )
	{
		byte[]? bytes = null;
		try
		{
			int length;
			using( FileStream file = File.OpenRead( path ) )
			{
				length = (int)file.Length;
				bytes = ArrayPool<byte>.Shared.Rent( length );
				file.ReadExactly( bytes, 0, length );
			}

			ReadOnlyMemory<byte> sourceData = bytes.AsMemory( 0, length );
			foreach( ReadOnlyMemory<byte> mem in LineParser.parse( sourceData ) )
				if( !mem.IsEmpty )
					yield return mem;
		}
		finally
		{
			if( null != bytes )
				ArrayPool<byte>.Shared.Return( bytes );
		}
	}
}