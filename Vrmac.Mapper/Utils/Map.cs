using MySqlConnector;
namespace Vrmac.Mapper;

/// <summary>Utility functions to define parameter types for the <see cref="iNamedParameters.parameters" /> property></summary>
public static class Map
{
	/// <summary><c>byte</c> parameter, or enum with byte base type</summary>
	/// <remarks>Note the behaviour depends on connection string setup, I am using <c>TreatTinyAsBoolean = false</c></remarks>
	public static ParameterInfo uint8( string name ) =>
		new ParameterInfo( name, MySqlDbType.Byte );
	/// <summary><c>ushort</c> parameter, or enum with ushort base type</summary>
	public static ParameterInfo uint16( string name ) =>
		new ParameterInfo( name, MySqlDbType.UInt16 );
	/// <summary><c>uint</c> parameter, or enum with uint base type</summary>
	public static ParameterInfo uint32( string name ) =>
		new ParameterInfo( name, MySqlDbType.UInt32 );
	/// <summary><c>int</c> parameter, or enum with int base type</summary>
	public static ParameterInfo int32( string name ) =>
		new ParameterInfo( name, MySqlDbType.Int32 );
	/// <summary><c>ulong</c> parameter</summary>
	public static ParameterInfo uint64( string name ) =>
		new ParameterInfo( name, MySqlDbType.UInt64 );

	/// <summary>Email parameter</summary>
	/// <remarks>The table schema should say something like <c>mail varchar(254) NOT NULL COLLATE utf8mb4_unicode_ci</c></remarks>
	public static ParameterInfo mail( string name = "mail" ) =>
		new ParameterInfo( name, MySqlDbType.VarChar, 254 );

	/// <summary>IP address parameter</summary>
	/// <remarks>The table schema should say something like <c>ip VARBINARY(16)</c></remarks>
	public static ParameterInfo ip( string name = "ip" ) =>
		new ParameterInfo( name, MySqlDbType.VarBinary, 16 );

	/// <summary>String parameter i.e. varchar column</summary>
	public static ParameterInfo str( string name, int len ) =>
		new ParameterInfo( name, MySqlDbType.VarChar, len );

	/// <summary>Fixed-length binary parameter</summary>
	/// <remarks>The table schema should use e.g. <c>binary(16)</c> column type</remarks>
	public static ParameterInfo bytes( string name, int cb ) =>
		new ParameterInfo( name, MySqlDbType.Binary, cb );
	/// <summary>Fixed-length binary parameter with 32 bytes</summary>
	public static ParameterInfo token( string name = "token" ) =>
		bytes( name, 32 );
	/// <summary>Timestamp parameter</summary>
	public static ParameterInfo timestamp( string name ) =>
		new ParameterInfo( name, MySqlDbType.Timestamp );
	/// <summary>JSON parameter</summary>
	public static ParameterInfo json( string name ) =>
		new ParameterInfo( name, MySqlDbType.JSON );
	/// <summary>Boolean parameter</summary>
	/// <remarks>Note the behaviour depends on connection string setup, I am using <c>TreatTinyAsBoolean = false</c></remarks>
	public static ParameterInfo boolean( string name ) =>
		new ParameterInfo( name, MySqlDbType.Bool );
}