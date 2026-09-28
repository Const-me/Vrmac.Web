using System.Text;
namespace Vrmac;

/// <summary>Output metadata for a single HTML template</summary>
readonly struct OutputMetadata
{
	public string name { get; }
	public string output { get; }
	public string ns { get; }
	public string template { get; }

	public OutputMetadata( in FileContext ctx )
	{
		name = Path.GetFileNameWithoutExtension( ctx.path );
		string[] folder = ctx.relativePath();

		// Name the generated file
		StringBuilder sb = new();
		sb.AppendJoin( "/", folder );
		if( sb.Length > 0 )
			sb.Append( "/" );
		sb.Append( name );
		int length = sb.Length;
		sb.Append( ".g.cs" );
		output = sb.ToString();

		sb.Length = length;
		sb.Append( ".html" );
		template = sb.ToString();

		// Namespace for the generated codes
		sb.Clear();
		sb.AppendJoin( ".", namespaceComponents( ctx.compilerOptions, folder ) );
		ns = sb.ToString();
	}

	static IEnumerable<string> namespaceComponents( CompilerOptions options, string[] folder )
	{
		if( options.namespaceFlags.HasFlag( eNamespaceFlags.Flat ) && folder.Length > 1 )
			Array.Resize( ref folder, 1 );

		if( options.namespaceFlags.HasFlag( eNamespaceFlags.UseRoot ) )
			yield return options.rootNamespace;

		foreach( string name in folder )
			yield return name;
	}
}