using MySqlConnector;
using System.Runtime.CompilerServices;
namespace Vrmac.Mapper;

/// <summary>Cached stuff to execute a particular query</summary>
/// <remarks>Probably the most important class in this entire DLL</remarks>
sealed class CachedCommand<T> where T : struct, iNamedParameters
{
	/// <summary>The SQL</summary>
	readonly string sql;

	/// <summary>Names and types of the parameters</summary>
	/// <remarks>Almost the same data as returned from <see cref="iNamedParameters.parameters" />.<br/>
	/// The only difference, names in this array include the <c>'@'</c> prefix</remarks>
	readonly ParameterInfo[] metadata;

	/// <summary>Reusable array of <see cref="MySqlParameter" /> objects for the command</summary>
	/// <remarks>Note each thread has an exclusive copy</remarks>
	[ThreadStatic]
	static ParamValues? cachedParams;

	/// <summary>This class only needs a single instance per generic argument</summary>
	static readonly CachedCommand<T> instance = new CachedCommand<T>();

	/// <summary>Create a command to execute the query, including all parameters</summary>
	public static MySqlCommand command( in Transaction transaction, in T args ) =>
		instance.createCommand( transaction, args );

	CachedCommand()
	{
		T val = default;
		sql = val.sql;
		metadata = val.parameters;
		convertParams( metadata );

		foreach( ParameterInfo param in metadata )
		{
			if( !sql.Contains( param.name ) )
				throw new ArgumentException( $"Parameter {param.name} not found in the SQL" );
		}
	}

	static void convertParams( ParameterInfo[] reflected )
	{
		HashSet<string> names = new HashSet<string>( reflected.Length, StringComparer.OrdinalIgnoreCase );
		for( int i = 0; i < reflected.Length; i++ )
		{
			ParameterInfo pi = reflected[ i ];
			string name = makeArgName( pi.name );
			if( !names.Add( name ) )
				throw new ArgumentException( "Parameter names should be unique" );
			reflected[ i ] = pi.setName( name );
		}
	}

	/// <summary>Throw an exception if the string is not a valid parameter name</summary>
	static string makeArgName( string name )
	{
		if( string.IsNullOrWhiteSpace( name ) )
			throw new ArgumentException( "Parameter name cannot be null or empty." );

		foreach( char c in name )
		{
			if( char.IsAsciiLetterOrDigit( c ) )
				continue;
			if( c == '_' )
				continue;
			throw new ArgumentException( $"Invalid character '{c}' in parameter name '{name}'." );
		}
		return "@" + name;
	}

	/// <summary>Create a command to execute the query, including all parameters</summary>
	MySqlCommand createCommand( in Transaction trans, in T args )
	{
		MySqlCommand command = trans.command();
		try
		{
			command.CommandText = sql;

			ParamValues values = makeParameters();
			// Set the object into the [ThreadStatic] destination, read by ParamsCollection parameterless constructor
			values.setupWriter();
			// ParamsCollection is a structure not a class,
			// The following line only allocates values boxed into `object? MySqlParameter.Value` properties
			using( ParamsCollection coll = args.values() )
				coll.checkExpectedLength();
			// Copy MySqlParameter objects into the command
			values.setupCommand( command.Parameters );
			return command;
		}
		catch
		{
			command.Dispose();
			throw;
		}
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	ParamValues makeParameters()
	{
		ParamValues? arr = cachedParams;
		if( null != arr )
			return arr;
		return createParameters();
	}

	[MethodImpl( MethodImplOptions.NoInlining )]
	ParamValues createParameters()
	{
		ParamValues res = new ParamValues( metadata );
		cachedParams = res;
		return res;
	}
}