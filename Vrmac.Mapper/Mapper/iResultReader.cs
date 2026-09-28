using MySqlConnector;
namespace Vrmac.Mapper;

/// <summary>Implement for a query which reads rows from data reader</summary>
public interface iResultReader<out T>
{
	/// <summary>Read fields from the current row of the reader into the output type</summary>
	T read( MySqlDataReader reader );
}