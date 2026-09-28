namespace Vrmac.PwnedFilter;

/// <summary>Utility class to compute summary of the source datasets</summary>
/// <remarks>The class is not thread safe</remarks>
sealed class SourceSummary
{
	public long lines { get; private set; }
	public int maxBreaches { get; private set; }
	public SourceSummary() { }

	/// <summary>Append a line parsed from a source text file</summary>
	public void addLine( ReadOnlySpan<byte> span )
	{
		if( span.IsEmpty )
			return;

		int idx = span.IndexOf( (byte)':' );
		if( idx <= 0 )
			throw new ArgumentException();
		span = span.Slice( idx + 1 );
		if( !int.TryParse( span, out int i ) )
			throw new ArgumentException();
		lines++;
		maxBreaches = Math.Max( maxBreaches, i );
	}

	/// <summary>Add a piece file; unlike the single file version, individual text files fit in RAM just fine.</summary>
	public void addFile( string folder, string name )
	{
		string path = Path.Combine( folder, name );
		foreach( ReadOnlyMemory<byte> mem in MultiFiles.parseLines( path ) )
			addLine( mem.Span );
	}

	/// <summary>Implements reduction step for <c>ParallelEnumerable.Aggregate</c></summary>
	public void addAnother( SourceSummary obj )
	{
		lines += obj.lines;
		maxBreaches = Math.Max( maxBreaches, obj.maxBreaches );
	}
}