namespace Vrmac.Uploads;

/// <summary>Some limits of the uploader</summary>
public struct UploaderLimits
{
	/// <summary>Maximum size of the uploaded file in bytes</summary>
	public long maxUploadSize;

	/// <summary>Minimum size of the uploaded file in bytes</summary>
	/// <remarks>The value should be well above 1 byte.<br/>
	/// A small minimum size allows to flood the disk with large count of small files.<br/>
	/// This degrades performance of directory scans, inflates size of directory metadata,<br/>
	/// and in the worst case takes down the entire file system by exhausting inode numbers.</remarks>
	public int minUploadSize;

	/// <summary>Maximum disk space in bytes</summary>
	/// <remarks>The value should be substantially smaller than disk capacity.<br/>
	/// Modern operating systems tend to fail spectacularly when their disk is full.</remarks>
	public long maxDiskSpace;

	/// <summary>Retention period of incomplete uploads</summary>
	public TimeSpan maxTempLifetime;
}