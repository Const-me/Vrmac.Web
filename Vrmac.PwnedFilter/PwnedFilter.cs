namespace Vrmac.PwnedFilter;

static class Program
{
	enum eCommand: byte
	{
		Summary,
		Build,
		Test,
	}

	static void mainImpl( string[] args )
	{
		if( args.Length == 0 )
			throw new ArgumentException( "Missing the command: Summary, Build or Test" );
		if( !Enum.TryParse( args[ 0 ], true, out eCommand command ) )
			throw new ArgumentException( "Unrecognized command" );

		switch( command )
		{
			case eCommand.Summary:
				printSummary( args );
				return;
			case eCommand.Build:
				Build.buildFilter( args );
				return;
			case eCommand.Test:
				Test.testFilter( args );
				return;
			default:
				throw new NotImplementedException();
		}
	}

	static void printSummary( string[] args )
	{
		if( args.Length < 2 )
			throw new ArgumentException( "Please specify the input text file" );

		string path = args[ 1 ];
		SourceSummary obj;
		if( File.Exists( path ) )
			obj = single( path );
		else if( Directory.Exists( path ) )
			obj = multi( path );
		else
			throw new ArgumentException( "The input is neither a file nor a directory: " + path );

		Console.WriteLine( "Count of entries: {0}", obj.lines );
		double gb = ( obj.lines * 20 ) * ( 1.0 / ( 1 << 30 ) );
		Console.WriteLine( "Combined size of SHA hashes: {0:F2}GB", gb );
		Console.WriteLine( "Maximum breaches count: {0}", obj.maxBreaches );

		static SourceSummary single( string path )
		{
			SourceSummary res = new();
			foreach( ReadOnlyMemory<byte> mem in TextReader.readLines( path ) )
				res.addLine( mem.Span );
			return res;
		}

		static SourceSummary multi( string path )
		{
			ParallelQuery<string> pq = MultiFiles.listFiles( path ).AsParallel();
			Func<SourceSummary> init = () => new SourceSummary();
			Func<SourceSummary, string, SourceSummary> update = ( SourceSummary acc, string name ) =>
			{
				acc.addFile( path, name );
				return acc;
			};
			Func<SourceSummary, SourceSummary, SourceSummary> combine = ( SourceSummary a, SourceSummary b ) =>
			{
				a.addAnother( b );
				return a;
			};
			Func<SourceSummary, SourceSummary> res = s => s;
			return pq.Aggregate( init, update, combine, res );
		}
	}

	static int Main( string[] args )
	{
		try
		{
			mainImpl( args );
			return 0;
		}
		catch( Exception ex )
		{
			Console.Error.WriteLine( ex.Message.Trim() );
			if( OperatingSystem.IsWindows() )
				return ex.HResult;
			return 1;
		}
	}
}