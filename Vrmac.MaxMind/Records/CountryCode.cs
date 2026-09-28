namespace Vrmac.MaxMind;

/// <summary>2 letter ISO 3166 country code packed into an integer in [ 0 .. 675 ] range for efficient comparisons and lookups</summary>
/// <seealso href="https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2" />
public readonly struct CountryCode
{
	public readonly ushort packed;
#if DEBUG
	readonly string str;
#endif
	CountryCode( ushort packed, string str )
	{
		this.packed = packed;
#if DEBUG
		this.str = str;
#endif
	}

	public static CountryCode invalid => new CountryCode( ushort.MaxValue, string.Empty );

	public static implicit operator bool( in CountryCode cc ) => cc.packed < capacity;


	public const int capacity = 26 * 26;
	public CountryCode( string str )
	{
		if( str.Length != 2 )
			throw new ArgumentException();

		int a = str[ 0 ];
		int b = str[ 1 ];
		a -= 'A';
		b -= 'A';

		if( ( a | b ) < 0 || a > 'Z' || b > 'Z' )
			throw new ArgumentException();

		int index = a * 26 + b;
		packed = (ushort)( index );
#if DEBUG
		this.str = str;
#endif
	}

	/// <summary>A string for debugger</summary>
	public override string ToString()
	{
		if( !this ) return "n/a";

		char c0 = (char)( ( packed / 26 ) + 'A' );
		char c1 = (char)( ( packed % 26 ) + 'A' );
		return $"{c0}{c1}";
	}
}