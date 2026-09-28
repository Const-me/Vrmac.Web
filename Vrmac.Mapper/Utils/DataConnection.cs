namespace Vrmac.Mapper;

/// <summary>Database connection string</summary>
public readonly struct DataConnection
{
	readonly string connStr;

	/// <summary>Create from connection string</summary>
	/// <remarks>Protip: use <see cref="MySqlConnector.MySqlConnectionStringBuilder" /> class to build these strings</remarks>
	public DataConnection( string connStr )
	{
		this.connStr = connStr;
	}

	internal Connection connection() => new Connection( connStr );

	/// <summary>Begin a transaction</summary>
	public Transaction transaction() => new Transaction( connStr );
}