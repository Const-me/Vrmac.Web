using MaxMind.Db;
using System.Net.Http.Headers;
namespace Vrmac.MaxMind;

/// <summary>High level functions to download and convert the OG databases</summary>
public static class MaxMindUpdate
{
	/// <summary>Load GeoLite2-Country database in <c>*.tar.gz</c> format; convert into another, more efficient binary format; serialise and gzip.</summary>
	/// <remarks>Returns converted and compressed database in array of bytes</remarks>
	public static byte[] convertDatabase( string sourcePath )
	{
		if( !sourcePath.EndsWith( ".tar.gz", StringComparison.OrdinalIgnoreCase ) )
			throw new ArgumentException( "Expected a *.tar.gz input path" );

		string name = Path.GetFileNameWithoutExtension( sourcePath );
		if( !name.StartsWith( "GeoLite2-Country", StringComparison.OrdinalIgnoreCase ) )
			throw new ArgumentException( "Expected a GeoLite2-Country input database" );

		using Reader reader = GeoLite.load( sourcePath );
		Reshape.Builder builder = reader.exportDatabase();
		return builder.serialise();
	}

	const string permalink = @"https://download.maxmind.com/geoip/databases/GeoLite2-Country/download?suffix=tar.gz";

	/// <summary>Download or update GeoLite2-Country database from maxmind.com</summary>
	/// <remarks>IANAL but I believe you should call this function at least once in 30 days. If you stop doing so, delete the cache folder.</remarks>
	/// <param name="cacheFolder">Absolute path to the local cache folder. The function should use under 10 mb disk space there.</param>
	/// <param name="auth">Base64-encoded <c>user:api_key</c> credentials</param>
	/// <returns>The string is absolute path to GeoLite2-Country database, a <c>*.tar.gz</c> file in the provided folder.<br/>
	/// The boolean is false when the local copy is already recent and no download took place</returns>
	public static async Task<(string path, bool downloaded)> updateIfNeeded( string cacheFolder, string auth )
	{
		using HttpClient client = new();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Basic", auth );

		ResponseInfo info;
		using( HttpResponseMessage response = await client.SendAsync( new HttpRequestMessage( HttpMethod.Head, permalink ) ) )
			info = new ResponseInfo( cacheFolder, response );

		FileInfo fi = new FileInfo( info.path );
		if( fi.Exists && fi.Length == info.length )
			return (info.path, false);

		string? pathTemp = null;
		try
		{
			using( HttpResponseMessage response = await client.GetAsync( permalink, HttpCompletionOption.ResponseHeadersRead ) )
			{
				info = new ResponseInfo( cacheFolder, response );
				pathTemp = Path.ChangeExtension( info.path, ".tmp" );
				using FileStream tmp = File.Create( pathTemp );
				await response.Content.CopyToAsync( tmp );
			}

			// https://www.maxmind.com/en/end-user-license-agreement
			// You shall cease use of and destroy (i) any old versions of the GeoIP Databases and GeoIP Data
			// within thirty (30) days following the release of the updated GeoIP Databases
			foreach( string path in Directory.EnumerateFiles( cacheFolder, "*.tar.gz" ) )
				File.Delete( path );

			File.Move( pathTemp, info.path );
		}
		finally
		{
			if( File.Exists( pathTemp ) )
				File.Delete( pathTemp );
		}

		return (info.path, true);
	}

	readonly struct ResponseInfo
	{
		public readonly long length;
		public readonly string path;
		public ResponseInfo( string cacheFolder, HttpResponseMessage response )
		{
			response.EnsureSuccessStatusCode();

			var headers = response.Content.Headers;
			long? length = headers.ContentLength;
			string? fileName = headers.ContentDisposition?.FileName;
			if( fileName == null || !length.HasValue )
				throw new ApplicationException( "Unable to download the database: missing response header" );
			if( fileName.ContainsAny( '\\', '/', ':' ) || fileName.Contains( ".." ) )
				throw new ApplicationException( "Unable to download the database: download.maxmind.com pwned" );

			this.length = length.Value;
			path = Path.Combine( cacheFolder, fileName );
		}
	}
}