namespace Vrmac.Admin;

/// <summary>Utility functions to print coloured text</summary>
public static class Print
{
	enum eMessageColour: byte
	{
		LogError,
		LogWarning,
		LogInfo,
		LogDebug,
		CommandSent,
		CommandOut,
		CommandError,
		Echo,
		Menu,
	}

	/// <summary>Surprisingly, cmd.exe default background ain’t black. At least not on Windows 10.</summary>
	const uint bgDefault = 0x0C0C0C;
	/// <summary>Background colour to distinguish remote stuff from local</summary>
	const uint bgRemote = 0x220055;

	static readonly string[] colours = [
		RGB(0xFF3355),	// LogError
		RGB(0xFFBB22),	// LogWarning
		RGB(0x33BB33),	// LogInfo
		RGB(0x7777FF),	// LogDebug
		RGB(0xAA77FF, bgRemote),	// CommandSent
		RGB(0x55FF00, bgRemote),	// CommandOut
		RGB(0xEE0011, bgRemote),	// CommandError
		RGB(0xAAEE00),	// Echo
		RGB(0x55DDDD)	// Menu
	];

	/// <summary>Translate <c>BGR8_UNORM</c> value into virtual terminal escape sequence with foreground and background colour</summary>
	internal static string RGB( uint bgr, uint bgcolour = bgDefault )
	{
		unchecked
		{
			byte r = (byte)( bgr >> 16 );
			byte g = (byte)( bgr >> 8 );
			byte b = (byte)( bgr );

			byte br = (byte)( bgcolour >> 16 );
			byte bg = (byte)( bgcolour >> 8 );
			byte bb = (byte)( bgcolour );

			return $"\u001b[38;2;{r};{g};{b};48;2;{br};{bg};{bb}m";
		}
	}

	const string resetColor = "\u001b[0m";

	internal ref struct RestoreColour
	{
		internal RestoreColour( byte idx )
		{
			eMessageColour mc = (eMessageColour)idx;
			lock( syncRoot )
			{
				if( stuckState != mc )
					Console.Out.Write( colours[ idx ] );
				stuckState = mc;
			}
		}
		public void Dispose()
		{
			lock( syncRoot )
			{
				stuckState = null;
				Console.Out.Write( resetColor );
			}
		}
	}

	static void print( eMessageColour idx, string message, bool keep = false )
	{
		lock( syncRoot )
		{
			if( stuckState != idx )
			{
				string color = colours[ (byte)idx ];
				Console.Out.Write( color );
			}
			Console.Out.WriteLine( message );
			if( keep )
				stuckState = idx;
			else
			{
				stuckState = null;
				Console.Out.Write( resetColor );
			}
		}
	}

	/// <summary>User prompt, same colour as default menus</summary>
	public static void prompt( string message ) => print( eMessageColour.Menu, message );
	/// <summary>Locally produced error message</summary>
	public static void error( string message ) => print( eMessageColour.LogError, message );
	/// <summary>Locally produced warning message</summary>
	public static void warning( string message ) => print( eMessageColour.LogWarning, message );
	/// <summary>Locally produced informational message</summary>
	public static void info( string message ) => print( eMessageColour.LogInfo, message );
	/// <summary>Locally produced debug message</summary>
	public static void debug( string message ) => print( eMessageColour.LogDebug, message );
	/// <summary>Echo of the command being launched on the remote computer over SSH session</summary>
	internal static void command( string message ) => print( eMessageColour.CommandSent, message, true );

	static readonly object syncRoot = new object();
	static eMessageColour? stuckState = null;

	// Unlike the rest of the messages, remote text arrives on random threads from the pool.
	// We assume that until an SSH command completes or fails, the GUI task is asleep waiting for it i.e. no other printed messages are interfering
	static void printRemote( ReadOnlySpan<char> message, eMessageColour idx )
	{
		lock( syncRoot )
		{
			if( stuckState != idx )
			{
				string color = colours[ (byte)idx ];
				Console.Out.Write( color );
			}
			Console.Out.Write( message );
			stuckState = idx;
		}
	}

	/// <summary>Standard output produced on the remote computer and delivered over the SSH session</summary>
	internal static void sshOutput( ReadOnlySpan<char> message ) =>
		printRemote( message, eMessageColour.CommandOut );

	/// <summary>Standard error produced on the remote computer and delivered over the SSH session</summary>
	internal static void sshError( ReadOnlySpan<char> message ) =>
		printRemote( message, eMessageColour.CommandError );

	/// <summary>Set up colours for keyboard echo; dispose the returned structure to restore.</summary>
	internal static RestoreColour echo() => new RestoreColour( (byte)eMessageColour.Echo );
}