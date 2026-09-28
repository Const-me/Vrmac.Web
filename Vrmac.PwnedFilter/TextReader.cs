namespace Vrmac.PwnedFilter;

static class TextReader
{
	/// <summary>Parse that 84.4GB ASCII text file into 4MB blocks split at line ends</summary>
	public static IEnumerable<ReadOnlyMemory<byte>> parseChunks( string path )
	{
		byte[] buffer = new byte[ 4 << 20 ];
		using FileStream file = File.OpenRead( path );
		long bytesLeft = file.Length;
		int movedTail = 0;

		while( true )
		{
			int cbRead = (int)Math.Min( bytesLeft, buffer.Length - movedTail );
			file.ReadExactly( buffer.AsSpan( movedTail, cbRead ) );
			bytesLeft -= cbRead;
			int cbPayload = movedTail + cbRead;
			int idx;
			if( bytesLeft > 0 )
			{
				idx = buffer.AsSpan( 0, cbPayload ).LastIndexOfAny( (byte)'\r', (byte)'\n' );
				if( idx < 0 )
					idx = cbPayload;
			}
			else
				idx = cbPayload;

			yield return buffer.AsMemory( 0, idx );

			if( bytesLeft > 0 )
			{
				movedTail = cbPayload - idx;
				buffer.AsSpan( idx, movedTail ).CopyTo( buffer.AsSpan( 0, movedTail ) );
			}
			else
				yield break;
		}
	}

	/// <summary>Parse that 84.4GB ASCII text file into lines</summary>
	public static IEnumerable<ReadOnlyMemory<byte>> readLines( string path )
	{
		foreach( ReadOnlyMemory<byte> chunk in parseChunks( path ) )
			foreach( ReadOnlyMemory<byte> mem in parseLines( chunk ) )
				yield return mem;
	}

	/// <summary>Slice a block of ASCII or UTF-8 text into a sequence of lines</summary>
	public static IEnumerable<ReadOnlyMemory<byte>> parseLines( ReadOnlyMemory<byte> mem )
	{
		while( !mem.IsEmpty )
		{
			int idx = mem.Span.IndexOfAnyExcept( (byte)'\r', (byte)'\n' );
			if( idx > 0 )
				mem = mem.Slice( idx );
			else if( idx < 0 )
				yield break;
			idx = mem.Span.IndexOfAny( (byte)'\r', (byte)'\n' );
			if( idx > 0 )
			{
				yield return mem.Slice( 0, idx );
				mem = mem.Slice( idx + 1 );
			}
			else if( idx < 0 )
			{
				yield return mem;
				yield break;
			}
			else
				throw new ApplicationException();
		}
	}
}