namespace Vrmac;

/// <summary>Callback interface to inject custom blocks into the markdown</summary>
public interface iMarkdownPlaceholders
{
	/// <summary>Write content of the custom block</summary>
	void write( StreamWriter w, string id );
}