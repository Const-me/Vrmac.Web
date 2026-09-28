using MySqlConnector;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Vrmac.Mapper;

/// <summary>Represents an in-progress transaction on a MySQL Server</summary>
[StructLayout( LayoutKind.Auto )]
public ref struct Transaction
{
	internal readonly Connection conn;
	readonly MySqlTransaction transaction;
	readonly bool ownsConnection;
#if DEBUG
	bool inTransaction;
#endif

	internal Transaction( string connectionString )
	{
		ownsConnection = true;
		conn = new Connection( connectionString );
		transaction = conn.beginTransaction();
#if DEBUG
		inTransaction = true;
#endif
	}

	internal Transaction( Connection conn )
	{
		ownsConnection = false;
		this.conn = conn;
		transaction = conn.beginTransaction();
#if DEBUG
		inTransaction = true;
#endif
	}

	/// <summary>Dispose the transaction</summary>
	/// <remarks>Note the library will rollback the transaction in this method, unless committed explicitly</remarks>
	public void Dispose()
	{
		transaction.Dispose();
		if( ownsConnection )
			conn.Dispose();
	}

	/// <summary>Commit the transaction</summary>
	public void commit()
	{
#if DEBUG
		Debug.Assert( inTransaction );
#endif
		transaction.Commit();
#if DEBUG
		inTransaction = false;
#endif
	}

	/// <summary>Create a new command to execute in this transaction</summary>
	internal MySqlCommand command()
	{
#if DEBUG
		Debug.Assert( inTransaction );
#endif
		MySqlCommand command = conn.command();
		command.Transaction = transaction;
		return command;
	}
}