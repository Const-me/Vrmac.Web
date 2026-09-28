namespace Vrmac.Mapper;

/// <summary>Implement on a structure to supply SQL query and the parameters</summary>
public interface iNamedParameters
{
	/// <summary>Array of values to insert into the query SQL</summary>
	/// <remarks>The ParamsCollection structure is designed for compatibility with C# collection expressions</remarks>
	ParamsCollection values();

	/// <summary>Types and names of the parameters</summary>
	/// <remarks>Should be stable across all instances of the structure.<br/>
	/// Use functions from the <see cref="Map" /> static class to create an array of them</remarks>
	ParameterInfo[] parameters { get; }

	/// <summary>SQL query</summary>
	/// <remarks>Should be stable across all instances of the structure.</remarks>
	string sql { get; }
}