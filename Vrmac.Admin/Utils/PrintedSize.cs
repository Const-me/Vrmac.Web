using System.Diagnostics;
using System.Runtime.InteropServices;
namespace Vrmac.Admin;

/// <summary>Data size with human-readable unit</summary>
[StructLayout( LayoutKind.Auto )]
public readonly struct PrintedSize
{
	internal readonly double size;
	internal readonly string unit;
	readonly bool printInteger;

	/// <summary>Create from int64 count of bytes</summary>
	public PrintedSize( long bytes )
	{
		Debug.Assert( bytes >= 0 );
		printInteger = false;
		if( bytes < 1 << 10 )
		{
			size = bytes;
			unit = "bytes";
			printInteger = true;
		}
		else if( bytes < 1 << 20 )
		{
			const double mul = 1.0 / ( 1 << 10 );
			size = mul * bytes;
			unit = "kb";
		}
		else if( bytes < 1 << 30 )
		{
			const double mul = 1.0 / ( 1 << 20 );
			size = mul * bytes;
			unit = "MB";
		}
		else
		{
			const double mul = 1.0 / ( 1 << 30 );
			size = mul * bytes;
			unit = "GB";
		}

		if( !printInteger && size > 75 )
		{
			size = (int)Math.Ceiling( size );
			printInteger = true;
		}
	}

	/// <summary>Convert into human-readable string</summary>
	public override string ToString()
	{
		if( printInteger )
			return $"{(int)size} {unit}";
		else
			return $"{size:F1} {unit}";
	}
}