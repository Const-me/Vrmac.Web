using System.Runtime.InteropServices;
namespace Vrmac.Uploads;

/// <summary>Utility functions to check for the quota</summary>
static class QuotaUtils
{
	/// <summary>Scan folder with completed uploads, return their total size</summary>
	/// <remarks>The server calls this on startup,
	/// then occasionally re-scans to detect files removed by a human operator with SFTP protocol</remarks>
	public static long completedBytes( string folder )
	{
		long cb = 0;
		foreach( string path in Directory.EnumerateFiles( folder ) )
		{
			FileInfo fi = new FileInfo( path );
			if( fi.Exists )
				cb += fi.Length;
		}
		return cb;
	}

	/// <summary>Scan folder with incomplete uploads, return combined size if/when they will complete</summary>
	/// <remarks>The server calls this once on startup before any HTTP requests are coming</remarks>
	public static long tempBytes( string folder )
	{
		long cb = 0;
		Span<byte> span = stackalloc byte[ 8 ];
		foreach( string path in Directory.EnumerateFiles( folder ) )
		{
			ReadOnlySpan<char> tmp = path;
			tmp = Path.GetExtension( tmp );
			if( tmp.Equals( ".tmp", StringComparison.OrdinalIgnoreCase ) )
				continue;

			try
			{
				using FileStream stream = File.OpenRead( path );
				stream.ReadExactly( span );
				cb += MemoryMarshal.Read<long>( span );
			}
			catch { }
		}
		return cb;
	}
}