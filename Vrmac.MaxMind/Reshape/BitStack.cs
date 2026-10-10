using System.Runtime.Intrinsics.X86;
namespace Vrmac.MaxMind.Reshape;

/// <summary>A stack of up to 128 bits</summary>
sealed class BitStack
{
	/// <summary>Lower and higher halves of the IPv6 subnet</summary>
	ulong a, b;
	public byte length { get; private set; }

	/// <summary>Push one bit to the stack</summary>
	public DepthRaii push( bool bit )
	{
		if( length >= 128 )
			throw new ArgumentOutOfRangeException();

		ulong one = bit ? 1u : 0u;
		if( length < 64 )
			a |= ( one << length );
		else
			b |= ( one << ( length - 64 ) );

		length++;
		return new DepthRaii( this );
	}

	void pop()
	{
		length--;
		if( length < 64 )
			a = Bmi2.X64.ZeroHighBits( a, length );
		else
			b = Bmi2.X64.ZeroHighBits( b, (ulong)( length - 64 ) );
	}

	public ref struct DepthRaii: IDisposable
	{
		BitStack stack;
		public DepthRaii( BitStack stack ) => this.stack = stack;
		public void Dispose() => stack.pop();
	}

	public override string ToString()
	{
		if( length > 96 && a == 0 && 0 == ( b & uint.MaxValue ) )
			return print4( (uint)( b >> 32 ), (byte)( length - 96 ) );
		return print6();
	}

	static string print4( uint addr, byte depth )
	{
		IPAddress ip = new IPAddress( rbit( addr ) );
		return $"{ip}/{depth}";

		static uint rbit( uint x )
		{
			x = ( ( x & 0x55555555 ) << 1 ) | ( ( x & 0xAAAAAAAA ) >> 1 );
			x = ( ( x & 0x33333333 ) << 2 ) | ( ( x & 0xCCCCCCCC ) >> 2 );
			x = ( ( x & 0x0F0F0F0F ) << 4 ) | ( ( x & 0xF0F0F0F0 ) >> 4 );
			return x;
		}
	}

	string print6()
	{
		Span<byte> span = stackalloc byte[ 16 ];
		BinaryPrimitives.WriteUInt64LittleEndian( span, rbit( a ) );
		BinaryPrimitives.WriteUInt64LittleEndian( span.Slice( 8 ), rbit( b ) );
		IPAddress ip = new IPAddress( span );
		return $"{ip}/{length}";

		static ulong rbit( ulong x )
		{
			x = ( ( x & 0x5555555555555555 ) << 1 ) | ( ( x & 0xAAAAAAAAAAAAAAAA ) >> 1 );
			x = ( ( x & 0x3333333333333333 ) << 2 ) | ( ( x & 0xCCCCCCCCCCCCCCCC ) >> 2 );
			x = ( ( x & 0x0F0F0F0F0F0F0F0F ) << 4 ) | ( ( x & 0xF0F0F0F0F0F0F0F0 ) >> 4 );
			return x;
		}
	}
}