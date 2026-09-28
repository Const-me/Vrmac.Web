using MySqlConnector;
namespace Vrmac.Mapper;

/// <summary>Metadata about one named parameter for an SQL query</summary>
public readonly struct ParameterInfo
{
	internal readonly string name;
	internal readonly MySqlDbType type;
	internal readonly int size;
	internal readonly string sourceColumn;

	/// <summary>Create the structure</summary>
	public ParameterInfo( string name, MySqlDbType type, int size = 0, string sourceColumn = "" )
	{
		this.name = name;
		this.type = type;
		this.size = size;
		this.sourceColumn = sourceColumn;
	}

	ParameterInfo( in ParameterInfo source, string name )
	{
		this = source;
		this.name = name;
	}
	internal ParameterInfo setName( string name ) => new ParameterInfo( this, name );
}