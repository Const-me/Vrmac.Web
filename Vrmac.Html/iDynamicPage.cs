using Microsoft.AspNetCore.Http;
using System.Net;
namespace Vrmac.Html;

/// <summary>Base interface for both dynamic and cached HTML pages</summary>
public interface iPageBase
{
	/// <summary>Render HTML content into the stream</summary>
	void render( Stream stream );

	/// <summary>Page title for the layout template, HTML escaped UTF-8</summary>
	ReadOnlySpan<byte> pageTitle { get; }
}

/// <summary>Interface for truly dynamic HTML pages generated on each request</summary>
/// <remarks>The design-time code generator in Vrmac.HtmlCompiler.dll adds this base interface to the generated dynamic page structures</remarks>
/// <seealso cref="DynamicPageRender" />
public interface iDynamicPage: iPageBase
{
	/// <summary>Initialise state fields for the request</summary>
	HttpStatusCode initialise( HttpContext context, object? state );
}

/// <summary>Interface for rarely changing but still technically dynamic HTML pages cached in memory</summary>
/// <remarks>The design-time code generator in Vrmac.HtmlCompiler.dll adds this base interface to the generated cached page structures</remarks>
/// <seealso cref="DynamicPageRender" />
public interface iStaticPage: iPageBase
{
	/// <summary>Initialise state fields for rendering a cached page</summary>
	HttpStatusCode initialise( object? state );
}