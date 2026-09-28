namespace Vrmac.Admin;

static class ConsoleUtils
{
	static readonly Dictionary<ConsoleKey, string> customNames = new()
	{
		[ ConsoleKey.Escape ] = "Esc",
		[ ConsoleKey.LeftArrow ] = "←",
		[ ConsoleKey.RightArrow ] = "→",
		[ ConsoleKey.UpArrow ] = "↑",
		[ ConsoleKey.DownArrow ] = "↓",
	};

	public static string print( this ConsoleKeyInfo key )
	{
		if( customNames.TryGetValue( key.Key, out var name ) )
			return name;

		if( char.IsAsciiLetterOrDigit( key.KeyChar ) )
		{
			char c = key.KeyChar;
			if( c == 'I' )
				c = 'i';
			else if( c != 'i' )
				c = char.ToUpperInvariant( c );
			return c.ToString();
		}

		switch( key.KeyChar )
		{
			case '\0':
			case '\t':
			case '\n':
			case '\r':
			case '\b':
				return key.Key.ToString();
		}
		return key.KeyChar.ToString();
	}

	public static string print( this ConsoleKey key )
	{
		if( customNames.TryGetValue( key, out var name ) )
			return name;
		return key.ToString();
	}
}