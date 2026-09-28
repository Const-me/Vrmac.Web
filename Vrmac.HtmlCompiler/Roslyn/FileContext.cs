using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
namespace Vrmac;

/// <summary>Utility structure to make batshit crazy Roslyn generator API at least slightly usable</summary>
readonly struct FileContext
{
	readonly SourceProductionContext spc;
	readonly InputFile file;
	readonly SourceText? sourceText;
	readonly string? fullText;
	readonly TextLineCollection? sourceLines;
	public CompilerOptions compilerOptions => file.options;

	public FileContext( SourceProductionContext spc, InputFile file )
	{
		this.spc = spc;
		this.file = file;
		sourceText = file.source.GetText( spc.CancellationToken );
		fullText = sourceText?.ToString();
		sourceLines = sourceText?.Lines;
	}

	/// <summary>False if there were errors reading the file, true when it's good</summary>
	public static implicit operator bool( in FileContext fc ) => null != fc.fullText;

	/// <summary>Full path to the source file on disk</summary>
	public string path => file.source.Path;

	/// <summary>Count of lines in the source file</summary>
	public int countLines => sourceLines!.Count;

	ReadOnlySpan<char> lineText( TextLine line )
	{
		TextSpan span = line.Span;
		return fullText!.AsSpan( span.Start, span.Length );
	}

	/// <summary>Extract one line of the source code, excluding the trailing line terminator</summary>
	public ReadOnlySpan<char> this[ int i ] => lineText( sourceLines![ i ] );

	/// <summary>Add generated file to the compilation</summary>
	public void addSource( MemoryStream ms, string tempFilePath )
	{
		ms.Seek( 0, SeekOrigin.Begin );
		SourceText sourceText = SourceText.From( ms, Encoding.UTF8, canBeEmbedded: true );
		spc.AddSource( tempFilePath, sourceText );
	}

	/// <summary>Report compilation error on the specified line of the source file</summary>
	public void reportError( DiagnosticDescriptor desc, int line, Exception ex ) =>
		reportError( desc, lineLocation( line ), ex );

	/// <summary>Report compilation error for the entire input file</summary>
	public void reportError( DiagnosticDescriptor desc, Exception ex ) =>
		reportError( desc, entireFileLocation(), ex );

	void reportError( DiagnosticDescriptor desc, Location loc, Exception ex )
	{
		Diagnostic diag = Diagnostic.Create( desc, loc, ex.Message );
		spc.ReportDiagnostic( diag );
	}

	/// <summary>Error location for specific line in the source template</summary>
	Location lineLocation( int i )
	{
		TextLine line = sourceLines![ i ];
		LinePositionSpan lps = new( new LinePosition( i, 0 ), new LinePosition( i, line.Span.Length ) );
		Location loc = Location.Create( path, line.Span, lps );
		return loc;
	}

	/// <summary>Error location for the entire source template</summary>
	Location entireFileLocation()
	{
		LinePositionSpan lps = new( new LinePosition( 0, 0 ), new LinePosition( 0, 0 ) );
		Location loc = Location.Create( path, new TextSpan( 0, 0 ), lps );
		return loc;
	}

	/// <summary>Folder of the input file relative to the project root, split into components</summary>
	/// <remarks>If the HTML template is directly at the root of the consuming project, the method will return empty array</remarks>
	public string[] relativePath()
	{
		string relative = Compat.relativePath( file.options.projectDir, path );
		relative = Path.GetDirectoryName( relative );
		string[] arr = relative.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
		if( arr.Length <= 0 )
			return Array.Empty<string>();

		List<string> list = new List<string>( arr.Length );
		foreach( string str in arr )
		{
			if( str == ".." || str == "." )
				continue;
			list.Add( str );
		}
		return list.ToArray();
	}
}