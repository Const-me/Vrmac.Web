// #define DBG_WINDOWS_DEVELOPMENT
// #define DBG_MEMORY_STATS
using System.Text;
namespace Vrmac.MaxMind;

readonly struct Status
{
	public readonly bool downloaded;
	public readonly string message;
	public Status( bool downloaded, string message )
	{
		this.downloaded = downloaded;
		this.message = message;
	}
}

static class Program
{
	static async Task<Status> mainImpl( string[] args )
	{
		if( args.Length != 1 )
			throw new ArgumentException( "Usage: Vrmac.MaxMind <cache directory>" );

		string? auth = Environment.GetEnvironmentVariable( Converter.envAuth );
		if( string.IsNullOrWhiteSpace( auth ) )
			throw new ArgumentException( $"Please pass MaxMind account in the {Converter.envAuth} environment variable" );

		string folder = args[ 0 ];
		if( !Directory.Exists( folder ) )
			Directory.CreateDirectory( folder );

		(string pathOg, bool convert) = await MaxMindUpdate.updateIfNeeded( folder, auth );
		convert = convert || needConvert( folder, pathOg );
		string nameOg = Path.GetFileName( pathOg );
		if( !convert )
			return new Status( false, "The cached DB still good: " + nameOg );

		byte[] convertedBytes = MaxMindUpdate.convertDatabase( pathOg );

		string tempPath = Path.Combine( folder, "geoip.tmp" );
		string path = Path.Combine( folder, Converter.result );
		File.WriteAllBytes( tempPath, convertedBytes );
		File.Move( tempPath, path, overwrite: true );

		path = Path.ChangeExtension( path, ".txt" );
		File.WriteAllText( tempPath, Path.GetFileName( pathOg ) );
		File.Move( tempPath, path, overwrite: true );

		return new Status( true, "Converted the MaxMind database: " + nameOg );
	}

	static bool needConvert( string folder, string og )
	{
		string bin = Path.Combine( folder, Converter.result );
		if( !File.Exists( bin ) )
			return true;

		string meta = Path.ChangeExtension( bin, ".txt" );
		if( !File.Exists( meta ) )
			return true;

		ReadOnlySpan<char> name = og;
		name = Path.GetFileName( name );
		string sourceName = File.ReadAllText( meta );
		return !name.Equals( sourceName, StringComparison.OrdinalIgnoreCase );
	}

#if DBG_WINDOWS_DEVELOPMENT
	static string[] dbgSetupDevelopment()
	{
		const string folder = @"C:\Temp\2remove\GeoIP";

		// The text file should contain single line like "0123456:<API key>" where 0123456 is your account ID
		string auth = Path.Combine( folder, "auth.txt" );
		if( !File.Exists( auth ) )
			throw new FileNotFoundException( "Maxmind auth token is missing, expected there: " + auth );
		auth = File.ReadAllText( auth ).Trim();
		auth = Convert.ToBase64String( Encoding.UTF8.GetBytes( auth ) );

		Environment.SetEnvironmentVariable( Converter.envAuth, auth, EnvironmentVariableTarget.Process );
		return [ folder ];
	}
#endif

#if DBG_MEMORY_STATS
	static void dbgPrintPeakMemoryUse()
	{
		double bytes = Process.GetCurrentProcess().PeakWorkingSet64;
		const double mulMegs = 1.0 / ( 1 << 20 );
		Console.WriteLine( "Peak memory use: {0:F2} MB", bytes * mulMegs );
	}
#endif

	static async Task<int> Main( string[] args )
	{
		try
		{
#if DBG_WINDOWS_DEVELOPMENT
			if( OperatingSystem.IsWindows() )
				args = dbgSetupDevelopment();
#endif
			Status result = await mainImpl( args );
			int code = result.downloaded ? 0 : 1;
			Console.Error.WriteLine( "{0}\t{1}", code, result.message );
#if DBG_MEMORY_STATS
			dbgPrintPeakMemoryUse();
#endif
			return 0;
		}
		catch( Exception ex )
		{
			int hr = ex.HResult;
			if( hr >= 0 )
				hr = new ApplicationException().HResult;
			Console.Error.WriteLine( "0x{0:X8}\t{1}", hr, ex.Message.Trim() );
			return 1;
		}
	}
}