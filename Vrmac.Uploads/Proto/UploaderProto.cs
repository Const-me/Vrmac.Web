namespace Vrmac.Uploads;

static class UploaderProto
{
	/// <summary>Maximum allowed chunk size</summary>
	public const int maxChunk = 2 << 20;

	// See Readme.md for the documentation

	/// <summary>Size of the upload request body</summary>
	internal const int uploadRequest = 32 + 16 + 16 + 64 + 8;
	/// <summary>Size of the upload response body, excluding the variable-length ECDSA signature immediately afterwards</summary>
	internal const int uploadResponseHeader = 32 + 16 + 8 + 2;
	/// <summary>Size of the chunk request body, excluding the variable-length payload data immediately afterwards</summary>
	internal const int chunkRequestHeader = 32 + 16 + 64 + 8 + 4;
}