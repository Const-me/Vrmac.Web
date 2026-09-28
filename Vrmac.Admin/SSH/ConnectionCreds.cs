using Renci.SshNet;
using System.Net;
using System.Net.Sockets;
namespace Vrmac.Admin;

/// <summary>Complete set of inputs to connect with SSH, SCP, or SFTP</summary>
public sealed class ConnectionCreds
{
	readonly string host;
	readonly int port;
	readonly string login;
	readonly byte[] keyBytes;
	readonly string? password;
	/// <summary>True when the host is across the internets as opposed to being on the same LAN</summary>
	public bool isRemote { get; }

	internal ConnectionCreds( string host, int port,
		string login, byte[] keyBytes, string? password )
	{
		this.host = host;
		this.port = port;
		this.login = login;
		this.keyBytes = keyBytes;
		this.password = password;
		isRemote = !isLocalAddress( host );
	}

	PrivateKeyFile loadKey()
	{
		MemoryStream stream = new( keyBytes, false );
		return new PrivateKeyFile( stream, password );
	}

	internal ConnectionInfo connectionInfo()
	{
		PrivateKeyFile key = loadKey();
		PrivateKeyAuthenticationMethod auth = new( login, key );
		return new ConnectionInfo( host, port, login, auth );
	}

	/// <summary>True when the remote host is actually local i.e. not across the internets</summary>
	static bool isLocalAddress( string str )
	{
		IPAddress ip = getAddress( str );
		if( IPAddress.IsLoopback( ip ) )
			return true;

		if( ip.AddressFamily == AddressFamily.InterNetworkV6 )
			return ip.IsIPv6LinkLocal || ( ip.GetAddressBytes()[ 0 ] & 0xFE ) == 0xFC; // fc00::/7, unique local

		byte[] b = ip.GetAddressBytes();
		return b[ 0 ] switch
		{
			10 => true,                           // 10.0.0.0/8
			172 => b[ 1 ] >= 16 && b[ 1 ] <= 31,  // 172.16.0.0/12
			192 => b[ 1 ] == 168,                 // 192.168.0.0/16
			169 => b[ 1 ] == 254,                 // 169.254.0.0/16, link-local
			_ => false,
		};
	}

	static IPAddress getAddress( string str )
	{
		IPAddress ip = impl( str );
		if( ip.IsIPv4MappedToIPv6 )
			ip = ip.MapToIPv4();
		return ip;

		static IPAddress impl( string str )
		{
			if( IPAddress.TryParse( str, out IPAddress? ip ) )
				return ip;
			IPAddress[] resolved = Dns.GetHostAddresses( str );
			if( resolved.Length == 0 )
				throw new ArgumentException( $"Unable to resolve host: {str}" );
			return resolved[ 0 ];
		}
	}
}