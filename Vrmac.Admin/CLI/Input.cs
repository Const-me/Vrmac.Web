using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace Vrmac.Admin;

/// <summary>Utility functions for handling console input</summary>
public static class Input
{
	/// <summary>Read a single key with echo</summary>
	public static ConsoleKeyInfo readKey()
	{
		ConsoleKeyInfo key = Console.ReadKey( intercept: true );
		using var col = Print.echo();
		Console.WriteLine( key.print() );
		return key;
	}

	/// <summary>Read a single line with password, using masked echo with <c>'*'</c> characters</summary>
	public static string readPassword()
	{
		using var col = Print.echo();
		List<char> password = new( 32 );
		while( true )
		{
			ConsoleKeyInfo key = Console.ReadKey( true );
			if( key.Key == ConsoleKey.Enter )
			{
				Console.WriteLine();
				break;
			}

			if( key.Key != ConsoleKey.Backspace )
			{
				password.Add( key.KeyChar );
				Console.Write( '*' );
				continue;
			}

			if( password.Count > 0 )
			{
				password.RemoveAt( password.Count - 1 );
				Console.Write( "\b \b" );
			}
		}

		Span<char> span = CollectionsMarshal.AsSpan( password );
		string result = new string( span );
		CryptographicOperations.ZeroMemory( MemoryMarshal.AsBytes( span ) );
		return result;
	}

	/// <summary>Read a single character with echo</summary>
	public static char readChar()
	{
		char ch;
		using( var col = Print.echo() )
			ch = Console.ReadKey().KeyChar;
		Console.WriteLine();
		return ch;
	}

	/// <summary>Read a line of text with echo</summary>
	public static string? readLine()
	{
		using var col = Print.echo();
		return Console.ReadLine();
	}
}