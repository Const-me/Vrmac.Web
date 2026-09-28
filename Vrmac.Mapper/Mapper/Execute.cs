using MySqlConnector;
using System.Data;
namespace Vrmac.Mapper;

/// <summary>Functions to execute various SQL queries in the context of transactions</summary>
public static class Execute
{
	/// <summary>Execute command, return count of affected rows</summary>
	public static int executeNonQuery<T>( this in Transaction transaction, in T args )
		where T : struct, iNamedParameters
	{
		using MySqlCommand command = CachedCommand<T>.command( transaction, args );
		return command.ExecuteNonQuery();
	}

	/// <summary>Execute SQL command which takes no parameters, return count of affected rows</summary>
	public static int executeNonQuery( this in Transaction transaction, string sql )
	{
		using MySqlCommand command = transaction.command();
		command.CommandText = sql;
		return command.ExecuteNonQuery();
	}

	/// <summary>Execute query with parameters, return first column of the first row</summary>
	public static ScalarValue executeScalar<T>( this in Transaction transaction, in T args )
		where T : struct, iNamedParameters
	{
		using MySqlCommand command = CachedCommand<T>.command( transaction, args );
		return new ScalarValue( command.ExecuteScalar() );
	}

	/// <summary>Execute SQL query which takes no parameters, return first column of the first row</summary>
	public static ScalarValue executeScalar( this in Transaction transaction, string sql )
	{
		using MySqlCommand command = transaction.command();
		command.CommandText = sql;
		return new ScalarValue( command.ExecuteScalar() );
	}

	/// <summary>Execute query which returns one record</summary>
	/// <remarks>When no records returned, the method throws an exception</remarks>
	public static TResult querySingle<T, TResult>( this in Transaction transaction, in T args )
		where T : struct, iNamedParameters, iResultReader<TResult>
	{
		using MySqlCommand command = CachedCommand<T>.command( transaction, args );
		using MySqlDataReader reader = command.ExecuteReader( CommandBehavior.SingleRow );
		if( reader.Read() )
			return args.read( reader );
		throw new InvalidOperationException( "The query has not returned any records" );
	}

	/// <summary>Execute query which returns zero or one record</summary>
	public static TResult? querySingleOrDefault<T, TResult>( this in Transaction transaction, in T args )
		where T : struct, iNamedParameters, iResultReader<TResult>
		where TResult : struct
	{
		using MySqlCommand command = CachedCommand<T>.command( transaction, args );
		using MySqlDataReader reader = command.ExecuteReader( CommandBehavior.SingleRow );
		if( reader.Read() )
			return args.read( reader );
		return null;
	}

	/// <summary>Execute query which returns a sequence of records</summary>
	/// <remarks>When the query doesn't return any rows, the function returns null</remarks>
	public static List<TResult>? query<T, TResult>( this in Transaction transaction, in T args )
		where T : struct, iNamedParameters, iResultReader<TResult>
	{
		List<TResult>? list = null;
		using MySqlCommand command = CachedCommand<T>.command( transaction, args );
		using MySqlDataReader reader = command.ExecuteReader( CommandBehavior.Default );
		while( reader.Read() )
		{
			list ??= new();
			list.Add( args.read( reader ) );
		}
		return list;
	}
}