namespace Vrmac.Admin;

/// <summary>Miscellaneous functions specific to managing Linux computers</summary>
public static class LinuxUtils
{
	/// <summary>Filter out <c>'\r'</c> bytes from the array of ASCII or UTF-8 bytes</summary>
	public static byte[] unixLineEndings( byte[] bytes )
	{
		ReadOnlySpan<byte> source = bytes;
		byte[] resultArray = new byte[ bytes.Length ];
		Span<byte> result = resultArray;

		const byte r = (byte)( '\r' );
		while( !source.IsEmpty )
		{
			int idx = source.IndexOf( r );
			if( idx > 0 )
			{
				source.Slice( 0, idx ).CopyTo( result );
				result = result.Slice( idx );
				source = source.Slice( idx + 1 );
				continue;
			}
			if( idx < 0 )
			{
				source.CopyTo( result );
				result = result.Slice( source.Length );
				break;
			}
			source = source.Slice( 1 );
		}

		int trimmed = result.Length;
		Array.Resize( ref resultArray, resultArray.Length - trimmed );
		return resultArray;
	}
}