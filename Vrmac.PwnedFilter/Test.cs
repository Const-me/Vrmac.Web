using Vrmac.Pwned;
namespace Vrmac.PwnedFilter;

static class Test
{
	public static void testFilter( string[] args )
	{
		if( args.Length < 2 )
			throw new ArgumentException( "Please specify the filter file" );
		using PwnedQuery filter = new( args[ 1 ] );

		while( true )
		{
			string? line = Console.ReadLine();
			if( string.IsNullOrEmpty( line ) )
				return;

			bool pwned = filter.query( line );
			using Colour raii = new( pwned ? ConsoleColor.Red : ConsoleColor.Green );
			if( pwned )
				Console.WriteLine( "Pwned!" );
			else
				Console.WriteLine( "OK" );
		}
	}

	ref struct Colour: IDisposable
	{
		readonly ConsoleColor restore;
		public Colour( ConsoleColor col )
		{
			restore = Console.ForegroundColor;
			Console.ForegroundColor = col;
		}
		public void Dispose() => Console.ForegroundColor = restore;
	}
}