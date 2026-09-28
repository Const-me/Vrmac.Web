using System.Runtime.CompilerServices;
namespace MaxMind.Db;

static class ReflectionUtil
{
	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	internal static void CheckType( Type expected, Type from )
	{
		if( expected == from )
			return;
		if( !expected.IsAssignableFrom( from ) )
			throw new DeserializationException( $"Could not convert '{from}' to '{expected}'." );
	}
}