using System.Runtime.InteropServices;
namespace Vrmac.Mapper;

/// <summary>Delegates to observe operation status of background jobs</summary>
[StructLayout( LayoutKind.Auto )]
public readonly struct BackgroundJobSink
{
	/// <summary>Create a sink to only observe fails</summary>
	public BackgroundJobSink( Action<string, Exception> fail )
	{
		this.fail = fail;
		success = null;
		logZeros = false;
	}

	/// <summary>Create a sink to observe both fails, and successfully completed jobs</summary>
	public BackgroundJobSink( Action<string, Exception> fail,
		Action<string, int> success, bool logZeros = false )
	{
		this.fail = fail;
		this.success = success;
		this.logZeros = logZeros;
	}
	/// <summary></summary>
	public void logFail( string what, Exception ex ) => fail( what, ex );

	/// <summary></summary>
	public void logSuccess( string what, int rows )
	{
		if( null == success )
			return;
		if( rows != 0 || logZeros )
			success( what, rows );
	}

	readonly Action<string, Exception> fail;
	readonly Action<string, int>? success;
	/// <summary></summary>
	public readonly bool logZeros;
}

/// <summary>A background maintenance job</summary>
public readonly struct BackgroundJob
{
	/// <summary>Function pointer to run a background job, and return count of modified rows</summary>
	/// <remarks>The function is expected to execute something like <c>DELETE FROM Table WHERE Condition</c></remarks>
	public delegate int pfnJob( in Transaction tx );

	/// <summary>Create the structure</summary>
	public BackgroundJob( pfnJob pfn, string name )
	{
		this.pfn = pfn;
		this.name = name;
	}

	readonly pfnJob pfn;
	readonly string name;

	/// <summary>Execute the jobs on a separate transaction, report status to the sink</summary>
	internal void run( in Connection conn, in BackgroundJobSink sink )
	{
		try
		{
			using Transaction tx = conn.transaction();
			int rows = pfn( tx );
			tx.commit();
			sink.logSuccess( name, rows );
		}
		catch( Exception ex )
		{
			sink.logFail( name, ex );
		}
	}
}

/// <summary>Utility function to run background maintenance tasks</summary>
public static class BackgroundJobExt
{
	/// <summary>Execute a sequence of background database maintenance jobs</summary>
	/// <remarks>The function never throws, handles all exceptions internally</remarks>
	public static void executeBackgroundJobs( this DataConnection dataConnection, BackgroundJob[] jobs, in BackgroundJobSink sink )
	{
		if( jobs == null || jobs.Length == 0 )
			return;

		try
		{
			// Open MySqlConnection
			using Connection conn = dataConnection.connection();

			// Execute these jobs using separate transactions for each one
			foreach( BackgroundJob job in jobs )
				job.run( conn, sink );
		}
		catch( Exception ex )
		{
			try
			{
				sink.logFail( nameof( executeBackgroundJobs ), ex );
			}
			catch { }
		}
	}
}