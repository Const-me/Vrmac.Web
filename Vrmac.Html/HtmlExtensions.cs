using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text;
namespace Vrmac.Html;

/// <summary>HTTP method of the request</summary>
public enum eRequestMethod: byte
{
	/// <summary><c>GET</c> method</summary>
	Get,
	/// <summary><c>POST</c> method</summary>
	Post,
	/// <summary>Some other method</summary>
	Other,
}

/// <summary>Utility functions to produce streams with UTF-8 HTML data</summary>
public static class HtmlExtensions
{
	/// <summary>Write a number encoded into Base64Url</summary>
	public static void compressed( this Stream stream, ulong number ) =>
		CompressedID.format( stream, number );

	/// <summary>Parse query string number encoded into Base64Url</summary>
	public static bool parseCompressedQuery( this HttpContext context, string key, out ulong number )
	{
		ReadOnlySpan<char> span = context.Request.Query[ key ];
		if( unchecked((uint)( span.Length - 1 ) < 12u) )
			return CompressedID.parse( span, out number );
		else
		{
			number = 0;
			return false;
		}
	}

	/// <summary>Parse query string number encoded into Base64Url</summary>
	public static ulong? parseCompressedQuery( this HttpContext context, string key )
	{
		if( parseCompressedQuery( context, key, out ulong number ) )
			return number;
		return null;
	}

	/// <summary>Write UTF-8 bytes with text, i.e. escape HTML and convert UTF-16 to UTF-8</summary>
	/// <remarks>Don’t call with very long strings, may crash with StackOverflowException.<br/>
	/// If you want to send megabytes of UTF-16 text, do something else</remarks>
	public static void text( this Stream stream, string? str )
	{
		if( string.IsNullOrEmpty( str ) )
			return;
		HtmlEncoder.write( stream, str );
	}

	/// <summary>Write UTF-8 bytes with text, replacing newlines with <c>&lt;br&gt;</c></summary>
	public static void errorMessageText( this Stream stream, string? str )
	{
		if( string.IsNullOrWhiteSpace( str ) )
			return;
		int idx = str.IndexOf( "\n" );
		if( idx < 0 )
		{
			HtmlEncoder.write( stream, str );
			return;
		}

		ReadOnlySpan<char> span = str;
		bool first = true;
		foreach( Range range in span.SplitAny( '\r', '\n' ) )
		{
			ReadOnlySpan<char> slice = span[ range ];
			if( slice.IsEmpty )
				continue;
			if( first )
				first = false;
			else
				stream.Write( "<br>"u8 );
			HtmlEncoder.write( stream, slice );
		}
	}

	/// <summary>Build an URL with 2 components, e.g. <c>/aaa/bbb</c></summary>
	/// <remarks>The arguments are UTF-8 and they must be URL safe, never user generated</remarks>
	public static void buildUrl( this Stream stream, ReadOnlySpan<byte> c0, ReadOnlySpan<byte> c1 )
	{
		stream.WriteByte( (byte)'/' );
		stream.Write( c0 );
		stream.WriteByte( (byte)'/' );
		stream.Write( c1 );
	}

	/// <summary>Build an URL with 3 components, e.g. <c>/aaa/bbb/ccc</c></summary>
	/// <remarks>The arguments are UTF-8 and they must be URL safe, never user generated</remarks>
	public static void buildUrl( this Stream stream,
		ReadOnlySpan<byte> c0, ReadOnlySpan<byte> c1, ReadOnlySpan<byte> c2 )
	{
		stream.WriteByte( (byte)'/' );
		stream.Write( c0 );
		stream.WriteByte( (byte)'/' );
		stream.Write( c1 );
		stream.WriteByte( (byte)'/' );
		stream.Write( c2 );
	}

	/// <summary>HTTP method of the request</summary>
	public static eRequestMethod requestMethod( this HttpContext context ) =>
		context.Request.Method switch
		{
			"GET" => eRequestMethod.Get,
			"POST" => eRequestMethod.Post,
			_ => eRequestMethod.Other,
		};

	/// <summary>Convert string to HTML encoded one, encode to UTF-8</summary>
	public static ReadOnlyMemory<byte> htmlEncoded( this string str ) =>
		HtmlEncoder.encode( str );

	/// <summary>Convert string to URL encoded one, encode to UTF-8</summary>
	public static ReadOnlyMemory<byte> urlEncoded( this string str )
	{
		str = WebUtility.UrlEncode( str );
		return Encoding.UTF8.GetBytes( str );
	}

	/// <summary>Convert ASCII string to bytes</summary>
	/// <remarks>The argument must be URL safe, never user generated</remarks>
	public static ReadOnlyMemory<byte> ascii( this string str ) =>
		Encoding.ASCII.GetBytes( str );

	/// <summary>Disable caching of the response</summary>
	public static void disableResponseCache( this HttpContext context )
	{
		IHeaderDictionary headers = context.Response.Headers;
		headers.CacheControl = "no-store, no-cache, must-revalidate";
		headers.Pragma = "no-cache";
		headers.Expires = "0";
	}
}