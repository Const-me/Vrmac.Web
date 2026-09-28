namespace Vrmac.Admin;

/// <summary>If you implement this interface in your menu class,<br/>
/// <see cref="MenuRunner" /> will check the property after each command,<br/>and quit the menu when it becomes true</summary>
public interface iMenu
{
	/// <summary>Return <c>true</c> to quit the currently running menu, <c>false</c> to stay there</summary>
	public bool shouldQuit { get; }
}

/// <summary>Execute menus</summary>
public static class MenuRunner
{
	static int menuLevel = -1;

	/// <summary>Run the menu</summary>
	public static async Task runMenu<T>( T impl ) where T : class
	{
		var items = MenuBuilder.build( typeof( T ) );
		menuLevel++;

		iMenu? menu = impl as iMenu;
		try
		{
			while( true )
			{
				bool again = await runMenu( impl, items );
				if( !again || true == menu?.shouldQuit )
					return;
			}
		}
		finally
		{
			menuLevel--;
		}
	}

	static async Task<bool> runMenu( object obj, MenuBuilder.Item[] items )
	{
		foreach( var item in items )
			Print.prompt( item.text );

		if( menuLevel == 0 )
			Print.prompt( "Esc: Quit the tool" );
		else
			Print.prompt( "Esc: Previous menu" );

		ConsoleKeyInfo key = Input.readKey();
		if( key.Key == ConsoleKey.Escape )
			return false;

		char lower = char.ToLower( key.KeyChar );
		foreach( var item in items )
		{
			if( !item.match( key ) )
				continue;

			try
			{
				await item.run( obj );
			}
			catch( Exception ex )
			{
				Print.error( ex.Message );
			}
			return true;
		}

		Print.warning( $"Unrecognized command \'{key.print()}'" );
		return true;
	}
}