using System.Security.Cryptography;
namespace Vrmac.Uploads;

/// <summary>Implement that interface to host the uploader component</summary>
/// <remarks>All properties are only read from the `Uploader` constructor; updates are ignored.</remarks>
public interface iUploaderHost
{
	/// <summary>If the request from that IP should complete ASAP, return a completed task.<br/>
	/// Otherwise, return the delay task to wait</summary>
	ValueTask rateLimit( IPAddress ip );

	/// <summary>Log a message</summary>
	/// <remarks>The status parameter is HRESULT; see section 2.1 of the [MS-ERREF] specification.</remarks>
	void logMessage( string what, int status, ReadOnlySpan<char> message, IPAddress ip );

	/// <summary>Folder on the disk to keep uploaded files</summary>
	/// <remarks>Incomplete ones are placed into <c>temp</c> subfolder inside that one</remarks>
	string storageRoot { get; }

	/// <summary>Some limits of the uploader</summary>
	UploaderLimits limits { get; }

	/// <summary>Private key to verify authenticity of the server</summary>
	/// <remarks>The uploader library assumes it has exclusive access to the object, protecting it with an internal lock.<br/>
	/// ECDsa class is not thread safe; do not reuse that key to sign stuff outside of this library.<br/>
	/// When null, the server will send zero-length signatures in upload or resume responses.</remarks>
	ECDsa? serverIdentityKey { get; }

	/// <summary>Message ID of the first request to start or resume an upload</summary>
	Guid idUploadRequest { get; }
	/// <summary>Message ID of the response to the <see cref="idUploadRequest" /> request</summary>
	Guid idUploadResponse { get; }
	/// <summary>Message ID of the request with the payload of some file being uploaded</summary>
	Guid idChunkRequest { get; }
}