using System.Reflection;
namespace Vrmac.Admin;

static class MenuBuilder
{
	public readonly struct Item
	{
		public readonly string text;
		public readonly Predicate<ConsoleKeyInfo> match;
		public readonly Func<object, Task> run;

		public Item( string text, Predicate<ConsoleKeyInfo> match, Func<object, Task> run, uint? colour )
		{
			this.match = match;
			this.run = run;
			if( colour.HasValue )
				this.text = Print.RGB( colour.Value ) + text;
			else
				this.text = text;
		}
	}

	public static Item[] build( Type type )
	{
		if( cache.TryGetValue( type, out var items ) )
			return items;
		items = buildMenu( type );
		cache.Add( type, items );
		return items;
	}

	static readonly Dictionary<Type, Item[]> cache = new();

	static Item[] buildMenu( Type type )
	{
		List<ItemReflection> list = reflect( type )
			.OrderBy( x => x.sortingKey )
			.ToList();

		Item[] arr = new Item[ list.Count ];
		int digits = 0;
		for( int i = 0; i < arr.Length; i++ )
		{
			eKeyBinding bind = list[ i ].keyBinding;
			if( bind == eKeyBinding.Unassigned )
			{
				if( digits > 10 )
					throw new ApplicationException( $"Too many commands in the menu {type.FullName}, please assign keys for some" );
				char key;
				if( digits < 9 )
					key = (char)( '1' + digits );
				else
					key = '0';
				digits++;
				arr[ i ] = makeItem( key, list[ i ] );
			}
			else if( bind == eKeyBinding.Char )
				arr[ i ] = makeItem( list[ i ].keyChar, list[ i ] );
			else
				arr[ i ] = makeItem( list[ i ].keyCode, list[ i ] );
		}
		return arr;
	}

	static Item makeItem( char key, in ItemReflection i )
	{
		string text = i.text;

		char displayKey;
		if( char.IsAsciiLetter( key ) && key != 'i' )
			displayKey = char.ToUpperInvariant( key );
		else
			displayKey = key;

		text = $"{displayKey}: {text}";
		char keyChar = key;
		Predicate<ConsoleKeyInfo> match = delegate ( ConsoleKeyInfo ck )
		{
			return keyChar == char.ToLowerInvariant( ck.KeyChar );
		};
		return new Item( text, match, makeFunction( i ), i.colour );
	}

	static Item makeItem( ConsoleKey key, in ItemReflection i )
	{
		string text = i.text;
		string displayKey = key.print();
		text = $"{displayKey}: {text}";

		ConsoleKey keyLocal = key;
		Predicate<ConsoleKeyInfo> match = delegate ( ConsoleKeyInfo ck )
		{
			return keyLocal == ck.Key;
		};
		return new Item( text, match, makeFunction( i ), i.colour );
	}

	static Func<object, Task> makeFunction( in ItemReflection i )
	{
		MethodInfo mi = i.mi;
		object[] args = Array.Empty<object>();
		if( i.async )
		{
			return delegate ( object obj )
			{
				return (Task)mi.Invoke( obj, args )!;
			};
		}
		else
		{
			return delegate ( object obj )
			{
				mi.Invoke( obj, args );
				return Task.CompletedTask;
			};
		}
	}

	enum eKeyBinding
	{
		Unassigned = 0,
		Char = 1,
		Code = 2,
	}

	readonly struct ItemReflection
	{
		public readonly ConsoleKey keyCode;
		public readonly char keyChar;
		public readonly eKeyBinding keyBinding;
		public readonly bool async;

		public readonly uint? colour;
		public readonly string text;
		public readonly MethodInfo mi;

		public ItemReflection( CommandAttribute attribute, MethodInfo mi, bool async )
		{
			byte mask = 0;
			if( attribute.keyChar != '\0' )
				mask = 1;
			if( attribute.keyCode != ConsoleKey.None )
				mask |= 2;
			if( mask == 3 )
				throw new ApplicationException();
			keyBinding = (eKeyBinding)mask;

			keyCode = attribute.keyCode;
			keyChar = attribute.keyChar;
			this.mi = mi;
			this.async = async;

			text = attribute.description;
			colour = mi.GetCustomAttribute<CommandColourAttribute>()?.bgr;
		}
		public int sortingKey => keyBinding == eKeyBinding.Unassigned ? -1 : 0;
	}

	static IEnumerable<ItemReflection> reflect( Type type )
	{
		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		foreach( var mi in type.GetMethods( flags ) )
		{
			CommandAttribute? a = mi.GetCustomAttribute<CommandAttribute>();
			if( a == null )
				continue;

			if( mi.GetParameters().Length > 0 )
				throw new ArgumentException( $"Menu command {type.FullName}.{mi.Name} should not take parameters" );

			bool async;
			Type tRet = mi.ReturnType;
			if( tRet == typeof( void ) )
				async = false;
			else if( tRet.IsAssignableTo( typeof( Task ) ) )
				async = true;
			else
				throw new ArgumentException( $"Menu command {type.FullName}.{mi.Name} should return either Task or void" );

			yield return new ItemReflection( a, mi, async );
		}
	}
}