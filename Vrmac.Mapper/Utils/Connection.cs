using MySqlConnector;
namespace Vrmac.Mapper;

/// <summary>A thin wrapper around <see cref="MySqlConnection"/> object</summary>
internal ref struct Connection
{
	readonly MySqlConnection connection;
	internal Connection( string connectionString )
	{
		connection = new MySqlConnection( connectionString );
		try
		{
			connection.Open();
		}
		catch
		{
			connection.Dispose();
			throw;
		}
	}

	/// <summary>Dispose the connection</summary>
	public void Dispose() => connection.Dispose();

	/// <summary>Create a new command to execute on this conection</summary>
	internal MySqlCommand command() => connection.CreateCommand();

	internal MySqlTransaction beginTransaction() =>
		connection.BeginTransaction();

	internal Transaction transaction() => new Transaction( this );
}