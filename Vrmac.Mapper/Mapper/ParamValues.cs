using MySqlConnector;
namespace Vrmac.Mapper;

sealed class ParamValues
{
	readonly MySqlParameter[] arr;

	internal ParamValues( ParameterInfo[] metadata )
	{
		arr = new MySqlParameter[ metadata.Length ];
		for( int i = 0; i < metadata.Length; i++ )
		{
			ParameterInfo pi = metadata[ i ];
			arr[ i ] = new MySqlParameter( pi.name, pi.type, pi.size, pi.sourceColumn );
		}
	}

	internal void setupWriter() =>
		ParamsCollection.setDestination( arr );

	internal void setupCommand( MySqlParameterCollection coll )
	{
		coll.Clear();
		foreach( MySqlParameter i in arr )
			coll.Add( i );
	}
}