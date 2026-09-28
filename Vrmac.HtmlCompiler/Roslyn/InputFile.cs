using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
namespace Vrmac;

sealed class InputFile
{
	public readonly AdditionalText source;
	public readonly CompilerOptions options;

	public Location lineLocation( int i, TextLine line )
	{
		LinePositionSpan lps = new( new LinePosition( i, 0 ), new LinePosition( i, line.Span.Length ) );
		Location loc = Location.Create( source.Path, line.Span, lps );
		return loc;
	}

	public Location entireFileLocation()
	{
		LinePositionSpan lps = new( new LinePosition( 0, 0 ), new LinePosition( 0, 0 ) );
		Location loc = Location.Create( source.Path, new TextSpan( 0, 0 ), lps );
		return loc;
	}

	InputFile( AdditionalText source, CompilerOptions options )
	{
		this.source = source;
		this.options = options;
	}

	static InputFile createItem( (AdditionalText, CompilerOptions) tuple, CancellationToken cancel ) =>
		new InputFile( tuple.Item1, tuple.Item2 );

	public static void generatorInit( IncrementalGeneratorInitializationContext context,
		Func<AdditionalText, bool> filter, Action<FileContext> impl )
	{
		IncrementalValuesProvider<AdditionalText> sourceFiles = context
			.AdditionalTextsProvider.Where( filter );

		Action<SourceProductionContext, InputFile> lambda = delegate ( SourceProductionContext spc, InputFile i )
		{
			FileContext fc = new( spc, i );
			impl( fc );
		};

		IncrementalValueProvider<CompilerOptions> options = context.AnalyzerConfigOptionsProvider.Select( CompilerOptions.create );

		context.RegisterSourceOutput( sourceFiles
			.Combine( options )
			.Select( createItem ), lambda );
	}
}