namespace Vrmac.Uploads;

/// <summary>Either non-negative integer, or HttpStatusCode enum</summary>
readonly struct OffsetOrStatus
{
	readonly long value;
	OffsetOrStatus( long value ) => this.value = value;

	/// <summary>Create with the offset</summary>
	public static implicit operator OffsetOrStatus( long offset )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( offset );
		return new OffsetOrStatus( offset );
	}

	/// <summary>Create with the status</summary>
	public static implicit operator OffsetOrStatus( HttpStatusCode code )
	{
		int i = (int)code;
		return new OffsetOrStatus( -i );
	}

	/// <summary>true when the structure has an offset</summary>
	public bool success => value >= 0;
	public long? offset => ( value >= 0 ) ? value : null;
	public HttpStatusCode? status => ( value < 0 ) ? (HttpStatusCode)( (int)-value ) : null;
}