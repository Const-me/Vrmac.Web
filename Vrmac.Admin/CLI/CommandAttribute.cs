namespace Vrmac.Admin;

/// <summary>Apply to methods of a menu class which take no arguments, and return either <c>void</c> or <c>Task</c></summary>
[AttributeUsage( AttributeTargets.Method )]
public sealed class CommandAttribute: Attribute
{
	internal string description { get; }
	internal char keyChar { get; }
	internal ConsoleKey keyCode { get; }

	/// <summary>Apply to a method of a menu class to automatically bind 1..10 number key; 10 is 0 because keyboard layout</summary>
	public CommandAttribute( string description )
	{
		this.description = description;
		keyChar = '\0';
		keyCode = ConsoleKey.None;
	}

	/// <summary>Apply to a method of a menu class to assign a letter key for the command</summary>
	public CommandAttribute( char keyChar, string description )
	{
		if( !char.IsAsciiLetter( keyChar ) )
			throw new ArgumentException();

		this.description = description;
		this.keyChar = char.ToLowerInvariant( keyChar );
		keyCode = ConsoleKey.None;
	}

	/// <summary>Apply to a method of a menu class to assign keys like F5 or arrow down for the command</summary>
	public CommandAttribute( ConsoleKey keyCode, string description )
	{
		this.description = description;
		keyChar = '\0';
		this.keyCode = keyCode;
	}
}