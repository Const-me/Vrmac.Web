# Vrmac.Html

This DLL implements runtime support for dynamic web pages
served from HTML templates compiled by the Vrmac.HtmlCompiler code generator.

### Motivation

Blazor pages aren’t terribly bad, but I don’t like the following things about them.

1. Most importantly, they are incompatible with AOT builds.\
For security reasons, on my web server I don’t want any JIT runtimes exposed to the internets.
2. To my taste, Blazor does too many memory allocations per request, also wastes too many CPU cycles encoding UTF-16 strings into UTF-8 responses.
3. Finally, Blazor screws up pretty printing of the HTML.

That’s why I implemented my own HTML templating library.

## Integration

Implement `iPageLayout` interface in some singleton class.

If you use dynamic pages, copy-paste the following class:

```C#
/// <summary>Generic function to render dynamic HTML pages, all of them.</summary>
/// <seealso cref="iDynamicPage" />
static class DynamicPage
{
	/// <summary>Render dynamic HTML page, send the response</summary>
	public static Task render<T>( HttpContext context, object? state ) where T : struct, iDynamicPage =>
		PageLayout.instance.sendPage<T>( context, state );

	/// <summary>Render dynamic HTML page, ideally with TLS protocol; send the response</summary>
	public static Task renderSecure<T>( HttpContext context, object? state ) where T : struct, iDynamicPage
	{
		if( context.redirectToTls() )
			return context.complete();
		return render<T>( context, state );
	}
}
```

If you use cached pages, copy-paste the following class:

```C#
/// <summary>Generic function to render rarely changing but still technically dynamic pages into arrays of bytes</summary>
static class CachedPage
{
	/// <summary>Render page into an array of bytes</summary>
	/// <remarks>Don't call per-request; use <see cref="DynamicPage" /> instead.</remarks>
	public static CachedResponse render<T>( DateTime modified, object? state ) where T : struct, iStaticPage =>
		PageLayout.instance.renderCached<T>( modified, state );
}
```

Note both classes depend on your iPageLayout implementation.\
That’s why they should be in your consuming project, instead of this DLL.

Finally, implement manually-written parts of the generated page structures.

Example for a cached page:

```C#
partial struct VersionHistory
{
	/// <summary>Call this exactly once on startup to register a route for the page</summary>
	public static void mapRoute( WebApplication app ) => app.MapGet( routePattern, handleGet );

	static Task handleGet( HttpContext context )
	{
		if( context.redirectToTls() )
			return context.complete();

		// That function throttle disk IO to 32 seconds min, returns immutable class with the state
		ReleaseState releases = Program.releases.getState();
		CachedResponse? cached = response;
		if( cached?.modified == releases.modified )
			return cached.send( context );

		return buildCache( context, releases );
	}

	[MethodImpl( MethodImplOptions.NoInlining )]
	static Task buildCache( HttpContext context, ReleaseState releases )
	{
		CachedResponse? cached;
		lock( syncRoot )
		{
			cached = response;
			if( cached?.modified != releases.modified )
			{
				// Note we pass `ReleaseState` object into `object? state` argument of the function
				cached = renderPage( releases.modified, releases );
				response = cached;
			}
		}
		return cached.send( context );
	}

	static CachedResponse? response = null;
	static readonly object syncRoot = new();

	HttpStatusCode iStaticPage.initialise( object? state )
	{
		// The cast is OK because buildCache passed that type
		ReleaseState releases = (ReleaseState)state!;

		if( null != releases.latestPublic() )
		{
			// Initialize the instance field of the structure
			model = new( releases );
			return HttpStatusCode.OK;
		}
		return HttpStatusCode.NotFound;
	}

	// The base interface automatically generated from VersionHistoryEntry.html
	readonly struct Entry: iVersionHistoryEntry
	{
		readonly ReadOnlyMemory<byte> version;
		readonly ReadOnlyMemory<byte> publishDate;
		public Entry( in Metadata meta )
		{
			version = meta.version;
			publishDate = meta.date;
		}
		public void ver( Stream stream ) => stream.write( version );
		public void date( Stream stream ) => stream.write( publishDate );
	}

	// The base interface automatically generated from VersionHistory.html
	readonly struct EntriesList: iVersionHistory
	{
		readonly ReleaseState releases;
		public EntriesList( ReleaseState releases ) => this.releases = releases;
		public void historyList( Stream stream )
		{
			foreach( Metadata meta in releases.enumeratePublic() )
			{
				// That applyTemplate function is generated from VersionHistoryEntry.html
				VersionHistoryEntry.applyTemplate( stream, new Entry( meta ) );
				// Template parser trims trailing whitespaces from HTML, including newline. Insert one manually.
				stream.newLine();
			}
		}
	}

	// The only instance field
	EntriesList model;

	// applyTemplate method is generated from VersionHistory.html
	void iPageBase.render( Stream stream ) => applyTemplate( stream, model );

	// Page title for Layout.html template
	ReadOnlySpan<byte> iPageBase.pageTitle => "Version History"u8;
}
```

Note the workflow.

1. Static request handlers passing some state object to the rendering method.
2. The first rendering callback is `HttpStatusCode iStaticPage.initialise()`\
When the method returns anything except HTTP 200 OK,
the calling `DynamicPageRender.renderCached<T>` will return failed status to the client.
3. Then the rendering method calls `iPageBase.render` which writes HTML document into the stream.

Note both `initialise` and `render` method aren't static, they are instance methods of the structure.\
The library expects you to setup some view state fields in the former, and use them in the latter to produce the HTML content.

The workflow for truly dynamic pages is very similar,
the only difference is the first method.\
Here’s a simple example.

```C#
partial struct HelloWorld
{
	public static void mapRoute( WebApplication app )
	{
		app.MapGet( routePattern, handleGet );
		static Task handleGet( HttpContext context ) => renderPage( context );
	}

	readonly struct Model: iHelloWorld
	{
		readonly DateTime dateTime;
		public Model( DateTime dateTime ) => this.dateTime = dateTime;
		void iHelloWorld.date( Stream stream ) => stream.text( dateTime.ToString( "s" ) );
		bool iHelloWorld.isSunday() => dateTime.DayOfWeek == DayOfWeek.Sunday;
	}

	Model model;
	HttpStatusCode iDynamicPage.initialise( HttpContext context, object? state )
	{
		model = new( DateTime.UtcNow );
		return HttpStatusCode.OK;
	}
	void iPageBase.render( Stream stream ) => applyTemplate( stream, model );
	ReadOnlySpan<byte> iPageBase.pageTitle => "Hello, world"u8;
}
```

Source template:

```HTML
@! route /hello-world
@! indent 1
<h1>Hello, World</h1>
<p>UTC Date: @date</p>
@{ isSunday
<p>It’s Sunday</p>
@}
```