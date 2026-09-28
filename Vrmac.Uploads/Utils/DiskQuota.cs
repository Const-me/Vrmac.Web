using System.Runtime.InteropServices;
namespace Vrmac.Uploads;

sealed class DiskQuota
{
	long diskUsage = 0;
	long expire = 0;
	// About 17 minutes
	const int msExpiration = 1024 * 1024;

	public void startup( string storageRoot, string storageTemp )
	{
		long diskUsage = 0;

		foreach( string path in Directory.EnumerateFiles( storageRoot ) )
		{
			FileInfo fi = new FileInfo( path );
			if( fi.Exists )
				diskUsage += fi.Length;
		}

		Span<byte> span = stackalloc byte[ 8 ];

		foreach( string path in Directory.EnumerateFiles( storageTemp ) )
		{
			ReadOnlySpan<char> pathSpan = path;
			pathSpan = Path.GetExtension( pathSpan );
			if( pathSpan.Equals( ".tmp", StringComparison.OrdinalIgnoreCase ) )
			{
				File.Delete( path );
				continue;
			}
			FileInfo fi = new FileInfo( path );
			if( !fi.Exists )
				continue;
			using( FileStream fs = fi.OpenRead() )
				fs.ReadExactly( span );
			diskUsage += MemoryMarshal.Read<long>( span );
		}

		this.diskUsage = diskUsage;
		expire = Environment.TickCount64 + msExpiration;
	}
}