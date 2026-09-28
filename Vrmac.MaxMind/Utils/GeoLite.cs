using MaxMind.Db;
using System.Formats.Tar;
using System.IO.Compression;
namespace Vrmac.MaxMind;

static class GeoLite
{
	public static Reader load( string tarGzip )
	{
		byte[] bytes = extractDatabase( tarGzip );
		var ms = new MemoryStream( bytes, false );
		return new Reader( ms );
	}

	static byte[] extractDatabase( string path )
	{
		using FileStream file = File.OpenRead( path );
		using GZipStream unzip = new( file, CompressionMode.Decompress );
		using TarReader reader = new( unzip );

		// Loop through all the entries in the tar
		while( reader.GetNextEntry() is TarEntry entry )
		{
			if( entry.EntryType != TarEntryType.RegularFile )
				continue;
			ReadOnlySpan<char> name = entry.Name;
			ReadOnlySpan<char> ext = Path.GetExtension( name );
			if( !ext.Equals( @".mmdb", StringComparison.OrdinalIgnoreCase ) )
				continue;

			using var ms = new MemoryStream( (int)entry.Length );
			using( Stream source = entry.DataStream! )
				source.CopyTo( ms );
			return ms.ToArray();
		}

		throw new ArgumentException( "The database is missing from the archive" );
	}
}