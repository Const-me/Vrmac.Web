namespace AcmeV2;

/// <summary>Status of the order</summary>
public readonly struct OrderStatus
{
	/// <summary>The status</summary>
	readonly eStatus status;
	/// <summary>When the status is ready, the URL to fetch the certificate</summary>
	public readonly string? location;
	/// <summary>Optional expiration date</summary>
	public readonly DateTime? expires;
	/// <summary>When the status is failed, error message</summary>
	public readonly string? fail;

	/// <summary>True when the certificate request completed successfully.</summary>
	public bool success => status.success();
	/// <summary>True when the certificate request has failed.</summary>
	public bool failed => status.failed();
	/// <summary>True when the certificate request is pending.</summary>
	public bool pending => status.pending();

	internal OrderStatus( Json.CertificateResponse response, bool throwIfFailed = true )
	{
		status = response.status;
		if( status.success() )
		{
			location = response.certificate;
			if( string.IsNullOrWhiteSpace( location ) )
				throw new ArgumentException( "The server sent successful status, but no location" );
		}
		expires = AcmeUtils.parseDateTime( response.expires );

		if( status.failed() )
		{
			fail = AcmeUtils.failMessage( status, response.error );
			if( throwIfFailed )
				throw new ApplicationException( fail );
		}
		else if( null != response.error )
			fail = AcmeUtils.failMessage( response.error );
	}
}