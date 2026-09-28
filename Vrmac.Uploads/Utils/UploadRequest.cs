using System.Runtime.InteropServices;
namespace Vrmac.Uploads;

[StructLayout( LayoutKind.Auto )]
readonly struct UploadRequest
{
	public readonly string name;
	public readonly long length;
	public readonly Guid nonce;

	public UploadRequest( string name, long length, in Guid nonce )
	{
		this.name = name;
		this.length = length;
		this.nonce = nonce;
	}
}