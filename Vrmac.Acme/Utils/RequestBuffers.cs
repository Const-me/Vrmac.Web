namespace AcmeV2;

sealed record class RequestBuffers
{
	readonly object syncRoot = new object();
	readonly List<RequestBuffer> list = new List<RequestBuffer>();

	public RentedBuffer rent()
	{
		RequestBuffer buffer;
		lock( syncRoot )
		{
			if( list.Count != 0 )
			{
				buffer = list[ list.Count - 1 ];
				list.RemoveAt( list.Count - 1 );
			}
			else
				buffer = new();
		}

		return new RentedBuffer( this, buffer );
	}

	public struct RentedBuffer: IDisposable
	{
		readonly RequestBuffers owner;
		public readonly RequestBuffer buffer;

		public RentedBuffer( RequestBuffers owner, RequestBuffer buffer )
		{
			this.owner = owner;
			this.buffer = buffer;
		}

		public void Dispose() =>
			owner.add( buffer );
	}

	void add( RequestBuffer buffer )
	{
		lock( syncRoot )
			list.Add( buffer );
	}
}