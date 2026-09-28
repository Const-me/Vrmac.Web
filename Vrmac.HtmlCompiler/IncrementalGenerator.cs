using Microsoft.CodeAnalysis;
namespace Vrmac;

/// <summary>Integrates the compiler into .NET build system</summary>
[Generator]
public sealed class Html: IIncrementalGenerator
{
	static bool isHtmlFile( AdditionalText text )
	{
		string? ext = Path.GetExtension( text.Path );
		if( string.IsNullOrWhiteSpace( ext ) )
			return false;
		return ext.Equals( ".html", StringComparison.OrdinalIgnoreCase );
	}

	void IIncrementalGenerator.Initialize( IncrementalGeneratorInitializationContext context ) =>
		InputFile.generatorInit( context, isHtmlFile, compileTemplate );

	static readonly DiagnosticDescriptor failParse = new( id: "HTML001",
		title: "Template Parse Error",
		messageFormat: "{0}",
		category: "Vrmac.HtmlCompiler",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true );

	static readonly DiagnosticDescriptor failGenerate = new(
		id: "HTML002",
		title: "Template Generation Error",
		messageFormat: "{0}",
		category: "Vrmac.HtmlCompiler",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true );

	static readonly DiagnosticDescriptor failMetadata = new(
		id: "HTML003",
		title: "Template Generation Error",
		messageFormat: "{0}",
		category: "Vrmac.HtmlCompiler",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true );

	static void compileTemplate( FileContext ctx )
	{
		if( !ctx )
			return;

		// Figure out output item metadata
		OutputMetadata meta;
		try
		{
			meta = new OutputMetadata( ctx );
		}
		catch( Exception ex )
		{
			ctx.reportError( failMetadata, ex );
			return;
		}

		// Parse the HTML
		SourceParser parser = new();
		for( int i = 0; i < ctx.countLines; i++ )
		{
			try
			{
				parser.addLine( ctx[ i ] );
			}
			catch( Exception ex )
			{
				ctx.reportError( failParse, i, ex );
				return;
			}
		}

		// Generate the source file
		MemoryStream ms;
		try
		{
			ms = parser.generate( meta.name, meta.ns, meta.template );
		}
		catch( Exception ex )
		{
			ctx.reportError( failGenerate, ex );
			return;
		}
		ctx.addSource( ms, meta.output );
	}
}