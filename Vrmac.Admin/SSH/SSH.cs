using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;
namespace Vrmac.Admin;

/// <summary>Utility functions for SSH remoting stuff</summary>
public static class SSH
{
	/// <summary>Parse host for the optional port number, load the private key,<br/>
	/// and if necessary ask user for the passphrase to decrypt the key</summary>
	public static ConnectionCreds connectionCreds( string host, string login, string pathPrivateKey )
	{
		host = host.Trim();
		int port = 22;
		int idx = host.IndexOf( ':' );
		if( idx > 0 )
		{
			port = int.Parse( host.Substring( idx + 1 ) );
			host = host.Substring( 0, idx );
		}

		byte[] bytes = File.ReadAllBytes( pathPrivateKey );
		string? password = keyPassword( bytes );
		return new ConnectionCreds( host, port, login, bytes, password );
	}

	static string? keyPassword( byte[] bytes )
	{
		try
		{
			using( MemoryStream stm = new( bytes, false ) )
			using( PrivateKeyFile key = new( stm ) )
				return null;
		}
		catch( SshPassPhraseNullOrEmptyException ) { }

		Print.prompt( "Please enter passphrase for the private key:" );
		string password = Input.readPassword();

		using( MemoryStream stm = new( bytes, false ) )
		using( PrivateKeyFile key = new( stm, password ) )
			return password;
	}

	const int connectTimeout = 2048;

	/// <summary>Connect with SSH protocol for executing remote commends</summary>
	public static async Task<SshClient> connect( ConnectionCreds creds )
	{
		ConnectionInfo conn = creds.connectionInfo();
		SshClient client = new( conn );
		try
		{
			using CancellationTokenSource cancelSource = new CancellationTokenSource();
			cancelSource.CancelAfter( connectTimeout );
			await client.ConnectAsync( cancelSource.Token );
			return client;
		}
		catch( OperationCanceledException )
		{
			client.Dispose();
			throw new TimeoutException( "SSH connection timeout" );
		}
		catch
		{
			client.Dispose();
			throw;
		}
	}

	/// <summary>Connect with SCP protocol for transfering files</summary>
	public static async Task<ScpClient> connectFiles( ConnectionCreds creds )
	{
		ScpClient scp = new( creds.connectionInfo(), RemotePathTransformation.None );
		try
		{
			using CancellationTokenSource cancelSource = new CancellationTokenSource();
			cancelSource.CancelAfter( connectTimeout );
			await scp.ConnectAsync( cancelSource.Token );
			return scp;
		}
		catch( OperationCanceledException )
		{
			scp.Dispose();
			throw new TimeoutException( "SCP connection timeout" );
		}
		catch
		{
			scp.Dispose();
			throw;
		}
	}

	/// <summary>Connect with SFTP protocol for transfering files in bulk</summary>
	public static async Task<SftpClient> connectSftp( ConnectionCreds creds )
	{
		SftpClient sftp = new SftpClient( creds.connectionInfo() );
		try
		{
			using CancellationTokenSource cancelSource = new CancellationTokenSource();
			cancelSource.CancelAfter( connectTimeout );
			await sftp.ConnectAsync( cancelSource.Token );
			return sftp;
		}
		catch( OperationCanceledException )
		{
			sftp.Dispose();
			throw new TimeoutException( "SFTP connection timeout" );
		}
		catch
		{
			sftp.Dispose();
			throw;
		}
	}

	abstract class Reader
	{
		readonly Stream stream;
		readonly byte[] buffer;
		readonly char[] characters;
		readonly Decoder decoder;
		public Task task { get; }
		const int bufferSize = 1024;
		protected Reader( Stream stream )
		{
			this.stream = stream;
			// UTF-8 decoder produces at most 1 character for each input byte.
			// Even if there was an incomplete character in the local state of the decoder.
			buffer = new byte[ bufferSize ];
			characters = new char[ bufferSize ];
			decoder = Encoding.UTF8.GetDecoder();

			task = mainLoop();
		}

		async Task mainLoop()
		{
			while( true )
			{
				int cb = await stream.ReadAsync( buffer );
				if( cb > 0 )
				{
					decodeBytes( buffer.AsSpan( 0, cb ), false );
					continue;
				}
				decodeBytes( ReadOnlySpan<byte>.Empty, true );
				return;
			}
		}

		void decodeBytes( ReadOnlySpan<byte> bytes, bool flush )
		{
			decoder.Convert( bytes, characters.AsSpan(), flush, out int cbDecoded, out int cc, out bool completed );
			if( cc > 0 )
				print( characters.AsSpan( 0, cc ) );
		}

		protected abstract void print( ReadOnlySpan<char> span );
	}

	sealed class OutputReader: Reader
	{
		public OutputReader( Stream stream ) : base( stream ) { }
		protected override void print( ReadOnlySpan<char> span ) =>
			Print.sshOutput( span );
	}

	sealed class ErrorReader: Reader
	{
		public ErrorReader( Stream stream ) : base( stream ) { }
		protected override void print( ReadOnlySpan<char> span ) =>
			Print.sshError( span );
	}

	readonly struct Command: IDisposable
	{
		readonly SshCommand command;
		readonly OutputReader stdout;
		readonly ErrorReader stderr;
		public Command( SshClient client, string cmd )
		{
			command = client.CreateCommand( cmd );
			stdout = new OutputReader( command.OutputStream );
			stderr = new ErrorReader( command.ExtendedOutputStream );
		}

		public void Dispose() => command.Dispose();
		public Task execute() => Task.WhenAll( command.ExecuteAsync(), stdout.task, stderr.task );
		public int exitStatus => command.ExitStatus!.Value;
	}

	/// <summary>Execute remote command over the SSH</summary>
	/// <remarks>By default, the function translates non-zero exit codes into exceptions</remarks>
	public static async Task command( this SshClient client, string commandText, bool ignoreStatus = false )
	{
		Print.command( commandText );
		using Command cmd = new( client, commandText );
		await cmd.execute();
		int code = cmd.exitStatus;
		if( code == 0 )
			return;
		if( !ignoreStatus )
			throw new ApplicationException( $"The command {commandText} failed with status {code}" );
	}

	/// <summary>Execute remote command over the SSH, and return exit code without translating into exception</summary>
	public static async Task<int> commandStatus( this SshClient client, string commandText )
	{
		Print.command( commandText );
		using Command cmd = new( client, commandText );
		await cmd.execute();
		return cmd.exitStatus;
	}

	/// <summary>Execute a sequence of remote commands one by one, translating non-zero exit codes into exceptions</summary>
	public static async Task commands( this SshClient client, params string[] commands )
	{
		foreach( string cmd in commands )
			await client.command( cmd );
	}

	/// <summary>Join multiple commands with <c> &amp;&amp; </c> and run them in one SSH command</summary>
	public static Task multiCommands( this SshClient client, params string[] commands )
	{
		string joined = string.Join( " && ", commands );
		return client.command( joined );
	}
}