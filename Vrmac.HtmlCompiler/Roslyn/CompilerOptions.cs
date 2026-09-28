using Microsoft.CodeAnalysis.Diagnostics;
namespace Vrmac;

[Flags]
enum eNamespaceFlags: byte
{
	None = 0,
	UseRoot = 1,
	Flat = 2,
}

sealed class CompilerOptions
{
	public string projectDir { get; }
	public string rootNamespace { get; }
	public eNamespaceFlags namespaceFlags { get; }

	CompilerOptions( AnalyzerConfigOptionsProvider options )
	{
		projectDir = globalOption( options, "build_property.ProjectDir" )
			?? throw new ApplicationException( "$ProjectDir property is missing" );
		rootNamespace = globalOption( options, "build_property.RootNamespace" ) ?? string.Empty;

		bool useRootNamespace = globalOptionBool( options, "build_property.VrmacHtmlRootNamespace" );
		if( string.IsNullOrWhiteSpace( rootNamespace ) )
			useRootNamespace = false;

		bool flatNamespace = globalOptionBool( options, "build_property.VrmacHtmlFlatNamespace" );

		eNamespaceFlags flags = eNamespaceFlags.None;
		if( useRootNamespace )
			flags |= eNamespaceFlags.UseRoot;
		if( flatNamespace )
			flags |= eNamespaceFlags.Flat;
		namespaceFlags = flags;
	}

	static string? globalOption( AnalyzerConfigOptionsProvider options, string key )
	{
		if( options.GlobalOptions.TryGetValue( key, out string? str ) )
			return str;
		return null;
	}

	static bool globalOptionBool( AnalyzerConfigOptionsProvider options, string key )
	{
		if( !options.GlobalOptions.TryGetValue( key, out string? str ) )
			return false;
		if( bool.TryParse( str, out bool result ) )
			return result;
		if( int.TryParse( str, out int i32 ) )
			return i32 != 0;
		throw new ArgumentException( "Malformed compiler option " + key );
	}

	public static CompilerOptions create( AnalyzerConfigOptionsProvider provider, CancellationToken cancel )
	{
		return new CompilerOptions( provider );
	}
}