static class RateLimitUtils
{
	public static ValueTask rateLimit( TimeSpan delay )
	{
		if( delay.Ticks <= 0 )
			return ValueTask.CompletedTask;
		return delayImpl( delay );
	}

	static async ValueTask delayImpl( TimeSpan delay ) =>
		await Task.Delay( delay );

	public static ValueTask rateLimit( TimeSpan delay, CancellationToken cancel )
	{
		if( delay.Ticks <= 0 )
		{
			cancel.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		return delayImpl( delay, cancel );
	}

	static async ValueTask delayImpl( TimeSpan delay, CancellationToken cancel ) =>
		await Task.Delay( delay, cancel );

	public static void forget( this Task task ) =>
		task.ContinueWith( t => forgetContinuation, TaskContinuationOptions.OnlyOnFaulted );

	/// <summary>Pre-allocated delegate for <see cref="forget"/> method to save GC allocations in runtime</summary>
	static readonly Action<Task> forgetContinuation = t => { _ = t.Exception; };
}