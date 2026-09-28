# Vrmac.Mapper

This library implements a micro-[ORM](https://en.wikipedia.org/wiki/Object%E2%80%93relational_mapping)
for [MariaDB](https://en.wikipedia.org/wiki/MariaDB)
heavily inspired by [Dapper](https://github.com/DapperLib/Dapper).

## Similarities

* The library doesn’t try to abstract away the [SQL](https://en.wikipedia.org/wiki/SQL)
* The library is built on top of [MySqlParameterCollection](https://mysqlconnector.net/api/mysqlconnector/mysqlparametercollectiontype/) API 
which prevents SQL injection attacks.

## Differences

* Unlike Dapper, this library is compatible with Native AOT. AOT compatibility was the only reason I built my own data access library.
* It’s designed to allocate very little heap memory per query.

## Limitations

There’s no async/await support.\
In my production environment MariaDB is configured with `skip-networking` option,
thus only accessible over [Unix sockets](https://en.wikipedia.org/wiki/Unix_domain_socket).
These sockets have exceptionally low latency, hence I have designed the entire data access library for synchronous operations.\
The library does work with TCP transport, but due to the lack of async/await I wouldn’t expect great performance over high latency networks.

At the time of writing, the only supported target framework is .NET 10.

Tested with MariaDB 11.8.5-11.8.8 server running on Alpine Linux, with Windows and Alpine Linux clients.

# Usage

Create a database with some tables.

Assemble a connection string for your database, here’s an example:
```C#
static class DatabaseCreds
{
	/// <summary>Build connection string for production use</summary>
	public static string productionUnix( string database, string user )
	{
		MySqlConnectionStringBuilder b = new();
		b.ConnectionProtocol = MySqlConnectionProtocol.UnixSocket;
		b.Server = "/run/mysqld/mysqld.sock";
		b.setCommon( database, user );
		return b.ConnectionString;
	}

	static void setCommon( this MySqlConnectionStringBuilder b, string database, string user )
	{
		b.Database = database;
		b.UserID = user;
		b.Pooling = true;
		b.MaximumPoolSize = (uint)Environment.ProcessorCount;
		b.DateTimeKind = MySqlDateTimeKind.Utc;
		b.TreatTinyAsBoolean = false;
		b.AllowUserVariables = true;
	}
}
```
Create `DataConnection` object from the string,
that type is a readonly structure with a single string field.

Write a structure which implements `iNamedParameters` interface,
and if you want to return records also `iResultReader<T>`

Begin a transaction by calling `DataConnection.transaction()` method.

Use extension methods from the `Execute` static class to run queries on the transaction.\
Commit the transaction if you want to save the changes.

## Example

Table schema:

```SQL
-- Cross-site request forgery tokens
CREATE TABLE CSRF
(
	token binary(16) NOT NULL COMMENT 'Single use token',
	expiration timestamp NOT NULL DEFAULT (UTC_TIMESTAMP + INTERVAL 1 HOUR) COMMENT 'Expiration time for the token',
	PRIMARY KEY (token) USING BTREE COMMENT 'Primary key B-tree index for record-level locks',
	INDEX idx_CSRF_expiration (expiration) USING BTREE COMMENT 'Another index for background maintenance task'
)
ENGINE=MEMORY COMMENT='One-time use tokens for CSRF protection; they expire in 1 hour, stored in RAM';
```

Functions implementing CRUD operations for that table:

```C#
/// <summary>SQL functions operating on the CSRF table</summary>
public static class CSRF
{
	/// <summary>Generate a new CSRF token</summary>
	public static byte[] csrfNewToken( this DataConnection conn )
	{
		const string sql = @"SET @tok = RANDOM_BYTES( 16 );
INSERT INTO CSRF( token ) VALUES ( @tok );
select @tok;";

		byte[]? token;
		using( var tx = conn.transaction() )
		{
			token = tx.executeScalar( sql ).bytes;
			tx.commit();
		}
		return token ?? throw new InvalidOperationException( "Failed to generate CSRF token" );
	}

	readonly struct VerifyToken: iNamedParameters
	{
		readonly byte[] token;
		public VerifyToken( byte[] token ) => this.token = token;
		public ParameterInfo[] parameters => [ Map.bytes( "tok", 16 ) ];
		public ParamsCollection values() => [ token ];
		// That `FOR UPDATE` in the query obtains a record lock before deleting the row
		// Makes csrfVerifyToken function thread safe despite no locks in C#
		public string sql =>
@"SET @result = 0;
SELECT expiration >= UTC_TIMESTAMP() INTO @result FROM CSRF WHERE token = @tok FOR UPDATE;
DELETE FROM CSRF WHERE token = @tok;
SELECT @result;";
	}

	/// <summary>Verify a token; note tokens are single use.</summary>
	public static bool csrfVerifyToken( this DataConnection conn, byte[] token )
	{
		Debug.Assert( token.Length == 16 );
		bool result;
		using( var tx = conn.transaction() )
		{
			result = tx.executeScalar( new VerifyToken( token ) ).boolean;
			tx.commit();
		}
		return result;
	}

	/// <summary>Delete all expired tokens. Returns count of erased rows.</summary>
	internal static int csrfRemoveExpired( in Transaction tx )
	{
		const string sql = "DELETE FROM CSRF WHERE expiration < UTC_TIMESTAMP()";
		return tx.executeNonQuery( sql );
	}
}
```