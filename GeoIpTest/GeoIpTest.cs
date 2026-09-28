using MaxMind.Db;
using System.Net;
using Vrmac.GeoIP;
using Vrmac.MaxMind;
namespace GeoIpTest;

static class Program
{
	const string folder = @"C:\Temp\2remove\GeoIP";

	static GeoPackage loadConverted()
	{
		string path = Path.Combine( folder, "geoip.bin" );
		return GeoPackage.load( path );
	}

	static Reader loadOriginal()
	{
		string str = Path.Combine( folder, "geoip.txt" );
		str = File.ReadAllText( str );
		str = Path.Combine( folder, str );
		return GeoLite.load( str );
	}

	const int countIp4Tests = 1024 * 1024;

	// IPv6 addresses of the port scanners from production log, LOL
	static readonly string[] ip6Strings =
	[
		"2604:a880:4:1d0::2fa6:b000",
		"2604:a880:800:14::5633:8000",
		"2604:a880:0:202a::b41:a000",
		"2604:a880:800:14::5633:9000",
		"2a06:4883:3000::44",
		"2a06:4882:3000::26",
		"2a06:4882:3000::2d",
		"2a06:4883:3000::2e",
		"2a06:4883:3000::40",
		"2604:a880:400:d1::91e4:c001",
		"2a06:4883:7000::79",
		"2a06:63c0:1902:a00:548:c333:6463:2034",
		"2604:a880:4:1d0::2fa6:c000",
	];

	static void Main( string[] args )
	{
		GeoPackage converted = loadConverted();
		using Reader original = loadOriginal();

		Random rng = new Random( 11 );
		Span<byte> span = stackalloc byte[ 4 ];

		Span<byte> spanMapped = stackalloc byte[ 16 ];
		spanMapped.Clear();
		spanMapped[ 0 ] = 0x20;
		spanMapped[ 1 ] = 0x02;

		for( int i = 0; i < countIp4Tests; i++ )
		{
			rng.NextBytes( span );
			IPAddress ip = new IPAddress( span );
			eRegion orig = original.lookup( ip );
			eRegion test = converted.lookup( ip );
			if( test != orig )
				throw new ApplicationException();

			IPAddress ip6 = ip.MapToIPv6();
			test = original.lookup( ip6 );
			if( test != orig )
				throw new ApplicationException();
			test = converted.lookup( ip6 );
			if( test != orig )
				throw new ApplicationException();

			span.CopyTo( spanMapped.Slice( 2, 4 ) );
			ip6 = new IPAddress( spanMapped );
			test = original.lookup( ip6 );
			if( test != orig )
				throw new ApplicationException();
			test = converted.lookup( ip6 );
			if( test != orig )
				throw new ApplicationException();
		}

		foreach( string str in ip6Strings )
		{
			IPAddress ip = IPAddress.Parse( str );
			eRegion test = converted.lookup( ip );
			eRegion orig = original.lookup( ip );
			if( test != orig )
				throw new ApplicationException();
		}

		Console.WriteLine( "OK" );
	}
}