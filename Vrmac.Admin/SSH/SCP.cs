using Renci.SshNet;
using System.IO.Compression;
namespace Vrmac.Admin;

/// <summary>Upload or download operation between local and remote computers</summary>
public readonly struct Transfer
{
	internal readonly string local, remote;
	Transfer( string local, string remote )
	{
		this.local = local;
		this.remote = remote;
	}

	/// <summary>Create from tuple of absolute paths</summary>
	/// <remarks>Don’t forget windows can use backslashes for path separator,<br/>while the rest of the OSes use forward slashes.<br/>
	/// Forward slashes work on windows too, but they never returned from <c>FileInfo</c> or file pickers or similar APIs.</remarks>
	public static implicit operator Transfer( (string local, string remote) val )
		=> new Transfer( val.local, val.remote );
}

/// <summary>Extension methods for <see cref="ScpClient" /> class from the <c>SSH.NET</c> package</summary>
public static class SCP
{
	/// <summary>Upload stream of bytes to remote computer</summary>
	public static void upload( this ScpClient scp, Stream source, string remote )
	{
		Print.debug( $"Uploading: {remote}" );
		scp.Upload( source, remote );
	}

	/// <summary>Upload array of bytes to remote computer</summary>
	public static void uploadBytes( this ScpClient scp, byte[] bytes, string remote )
	{
		using var stm = new MemoryStream( bytes, false );
		upload( scp, stm, remote );
	}

	/// <summary>Upload local file to remote computer</summary>
	public static void upload( this ScpClient scp, string local, string remote )
	{
		using var source = File.OpenRead( local );
		scp.upload( source, remote );
	}

	/// <summary>Compress a file on local PC with gzip, upload to remote computer</summary>
	public static void uploadGzip( this ScpClient scp, string local, string remote )
	{
		using MemoryStream ms = new();
		PrintedSize sizeLocal;
		using( FileStream source = File.OpenRead( local ) )
		using( GZipStream gzip = new( ms, CompressionLevel.SmallestSize, true ) )
		{
			sizeLocal = new( source.Length );
			source.CopyTo( gzip );
		}
		PrintedSize sizeCompressed = new( ms.Length );
		Print.debug( $"GZip compressor: {sizeLocal} -> {sizeCompressed}" );

		ms.Seek( 0, SeekOrigin.Begin );
		scp.upload( ms, remote );
	}

	/// <summary>Upload multiple local files to remote computer</summary>
	public static void upload( this ScpClient scp, IEnumerable<Transfer> xfers )
	{
		foreach( Transfer xf in xfers )
		{
			Print.debug( $"Uploading: {xf.remote}" );
			FileInfo sourceFile = new FileInfo( xf.local );
			scp.Upload( sourceFile, xf.remote );
		}
	}

	/// <summary>Download a file from the remote computer, save to local disk</summary>
	public static void download( this ScpClient scp, string local, string remote )
	{
		Print.debug( $"Downloading: {remote}" );
		using Stream file = File.Create( local );

		using var ms = new MemoryStream();
		scp.Download( remote, ms );

		ms.Seek( 0, SeekOrigin.Begin );
		ms.CopyTo( file );
	}

	/// <summary>Download multiple files from the remote computer, save to local disk</summary>
	public static void download( this ScpClient scp, IEnumerable<Transfer> list )
	{
		foreach( Transfer i in list )
			scp.download( i.local, i.remote );
	}

	/// <summary>Download a file from the remote computer, return bytes in memory</summary>
	public static byte[] downloadBytes( this ScpClient scp, string remote )
	{
		Print.debug( $"Downloading: {remote}" );
		using var ms = new MemoryStream();
		scp.Download( remote, ms );
		return ms.ToArray();
	}
}