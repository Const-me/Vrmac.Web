// Adapted from MaxMind-DB-Reader-dotnet 5.0, slightly modified on 2026-05-13
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
namespace MaxMind.Db;

internal sealed class MemoryMapBuffer: IDisposable
{
	readonly MemoryMappedFile _memoryMappedFile;
	readonly MemoryMappedViewAccessor _view;
	readonly nint _ptr;
	bool _disposed;
	internal long Length { get; }

	// Creates a named memory-mapped file backed directly by the file on
	// disk, suitable for cross-process sharing.
	internal MemoryMapBuffer( string file, bool useGlobalNamespace ) : this( file, useGlobalNamespace, new FileInfo( file ) )
	{
	}

	private MemoryMapBuffer( string file, bool useGlobalNamespace, FileInfo fileInfo )
	{
		using FileStream stream = new( file, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read );
		Length = stream.Length;
		// Ideally we would use the file ID in the mapName, but it is not
		// easily available from C#.
		var objectNamespace = useGlobalNamespace ? "Global" : "Local";

		// We create a sha256 here as there are limitations on mutex names.
		using var sha256 = SHA256.Create();
		var suffixTxt = $"{fileInfo.FullName.Replace( "\\", "-" )}-{Length}";
		var hashBytes = sha256.ComputeHash( Encoding.UTF8.GetBytes( suffixTxt ) );
		var suffix = BitConverter.ToString( hashBytes ).Replace( "-", "" );

		var mapName = $"{objectNamespace}\\{suffix}";
		var mutexName = $"{mapName}-Mutex";

		using( Mutex mutex = new Mutex( false, mutexName ) )
		{
			var hasHandle = false;

			try
			{
				hasHandle = mutex.WaitOne( TimeSpan.FromSeconds( 10 ), false );
				if( !hasHandle )
					throw new TimeoutException( "Timeout waiting for mutex." );

				if( OperatingSystem.IsWindows() )
					_memoryMappedFile = MemoryMappedFile.OpenExisting( mapName, MemoryMappedFileRights.Read );
				else
					throw new PlatformNotSupportedException();
			}
			catch( Exception ex ) when( ex is IOException or NotImplementedException or PlatformNotSupportedException )
			{
				// In .NET Core, named maps are not supported for Unices yet: https://github.com/dotnet/corefx/issues/1329
				// When executed on unsupported platform, we get the PNSE. In which case, we construct the memory map by
				// setting mapName to null.
				if( ex is PlatformNotSupportedException )
					mapName = null;

				_memoryMappedFile = MemoryMappedFile.CreateFromFile( stream, mapName, Length,
						MemoryMappedFileAccess.Read, HandleInheritability.None, false );
			}
			finally
			{
				if( hasHandle )
					mutex.ReleaseMutex();
			}
		}

		_view = _memoryMappedFile.CreateViewAccessor( 0, Length, MemoryMappedFileAccess.Read );
		_ptr = AcquireRawPointer();
	}

	// Reads the file into an anonymous memory-mapped region that is
	// private to this process.
	internal MemoryMapBuffer( string file )
	{
		using FileStream stream = new( file, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read );
		Length = stream.Length;
		(_memoryMappedFile, _view) = CreateMmapFromStream( stream, Length );
		_ptr = AcquireRawPointer();
	}

	// Reads the stream into an anonymous memory-mapped region that is
	// private to this process.
	internal MemoryMapBuffer( Stream stream )
	{
		if( stream == null )
			throw new ArgumentNullException( nameof( stream ), "The database stream must not be null." );

		if( stream.CanSeek )
		{
			Length = stream.Length - stream.Position;

			(_memoryMappedFile, _view) = CreateMmapFromStream( stream, Length );
			_ptr = AcquireRawPointer();
			return;
		}

		var tempFile = Path.GetTempFileName();
		try
		{
			using( FileStream tempStream = new( tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None ) )
			{
				stream.CopyTo( tempStream );
				Length = tempStream.Length;

				tempStream.Position = 0;
				(_memoryMappedFile, _view) = CreateMmapFromStream( tempStream, Length );
				_ptr = AcquireRawPointer();
			}
		}
		finally
		{
			try
			{
				File.Delete( tempFile );
			}
			catch
			{
				// Best-effort cleanup. If deletion fails, the temp
				// file is orphaned but the mmap may already be valid.
				// Letting this exception propagate would turn a
				// successful construction into a failure and leak the
				// mmap resources.
			}
		}
	}

	private MemoryMapBuffer( MemoryMappedFile memoryMappedFile, MemoryMappedViewAccessor view, long length )
	{
		Length = length;
		_memoryMappedFile = memoryMappedFile;
		_view = view;
		_ptr = AcquireRawPointer();
	}

	internal static async Task<MemoryMapBuffer> CreateAsync( string file )
	{
		using FileStream stream = new( file, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read, 4096, true );
		return await CreateAsync( stream ).ConfigureAwait( false );
	}

	internal static async Task<MemoryMapBuffer> CreateAsync( Stream stream )
	{
		if( stream == null )
			throw new ArgumentNullException( nameof( stream ), "The database stream must not be null." );

		if( stream.CanSeek )
		{
			long length = stream.Length - stream.Position;
			var (memoryMappedFile, view) = await CreateMmapFromStreamAsync( stream, length ).ConfigureAwait( false );
			return new MemoryMapBuffer( memoryMappedFile, view, length );
		}

		var tempFile = Path.GetTempFileName();
		try
		{
			using( var tempStream = new FileStream( tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, true ) )
			{
				await stream.CopyToAsync( tempStream ).ConfigureAwait( false );
				var length = tempStream.Length;

				tempStream.Position = 0;
				var (memoryMappedFile, view) = await CreateMmapFromStreamAsync( tempStream, length ).ConfigureAwait( false );

				return new MemoryMapBuffer( memoryMappedFile, view, length );
			}
		}
		finally
		{
			try
			{
				File.Delete( tempFile );
			}
			catch
			{
				// Best-effort cleanup. If deletion fails, the temp
				// file is orphaned but the mmap may already be valid.
				// Letting this exception propagate would turn a
				// successful construction into a failure and leak the
				// mmap resources.
			}
		}
	}

	private static (MemoryMappedFile File, MemoryMappedViewAccessor View) CreateMmapFromStream( Stream source, long length )
	{
		if( length == 0 )
			throw new InvalidDatabaseException( "The database is empty." );

		MemoryMappedFile memoryMappedFile = MemoryMappedFile.CreateNew( null, length );
		try
		{
			using( var viewStream = memoryMappedFile.CreateViewStream( 0, length, MemoryMappedFileAccess.Write ) )
				source.CopyTo( viewStream );
			var view = memoryMappedFile.CreateViewAccessor( 0, length, MemoryMappedFileAccess.Read );
			return (memoryMappedFile, view);
		}
		catch
		{
			memoryMappedFile.Dispose();
			throw;
		}
	}

	private static async Task<(MemoryMappedFile File, MemoryMappedViewAccessor View)> CreateMmapFromStreamAsync( Stream source, long length )
	{
		if( length == 0 )
			throw new InvalidDatabaseException( "The database is empty." );

		MemoryMappedFile memoryMappedFile = MemoryMappedFile.CreateNew( null, length );
		try
		{
			using( var viewStream = memoryMappedFile.CreateViewStream( 0, length, MemoryMappedFileAccess.Write ) )
				await source.CopyToAsync( viewStream ).ConfigureAwait( false );
			var view = memoryMappedFile.CreateViewAccessor( 0, length, MemoryMappedFileAccess.Read );
			return (memoryMappedFile, view);
		}
		catch
		{
			memoryMappedFile.Dispose();
			throw;
		}
	}

	private unsafe nint AcquireRawPointer()
	{
		try
		{
			byte* ptr = null;
			_view.SafeMemoryMappedViewHandle.AcquirePointer( ref ptr );
			return (IntPtr)( ptr + _view.PointerOffset );
		}
		catch
		{
			_view.Dispose();
			_memoryMappedFile.Dispose();
			throw;
		}
	}

	// Returns a bounds-checked Span over the requested region of the
	// memory-mapped buffer. This restores CLR bounds checking that raw
	// pointer access removed, at negligible cost (~1 cmp per index).
	// Uses a targeted slice rather than spanning the full buffer so
	// that databases larger than 2 GiB still work (Span length is int).
	internal unsafe ReadOnlySpan<byte> GetSpan( long offset, int count )
	{
		if( offset < 0 || (ulong)offset + (ulong)count > (ulong)Length )
			throw new ArgumentOutOfRangeException( nameof( offset ), "Attempt to read beyond the end of the MemoryMappedFile." );
		return new ReadOnlySpan<byte>( (byte*)_ptr + offset, count );
	}

	internal byte[] Read( long offset, int count )
	{
		ObjectDisposedException.ThrowIf( _disposed, this );
		return GetSpan( offset, count ).ToArray();
	}

	internal byte ReadOne( long offset )
	{
		ObjectDisposedException.ThrowIf( _disposed, this );

		if( (ulong)offset >= (ulong)Length )
		{
			throw new ArgumentOutOfRangeException( nameof( offset ),
				"Attempt to read beyond the end of the MemoryMappedFile." );
		}
		unsafe
		{
			return *( ( (byte*)_ptr ) + offset );
		}
	}

	internal string ReadString( long offset, int count )
	{
		ObjectDisposedException.ThrowIf( _disposed, this );
		return Encoding.UTF8.GetString( GetSpan( offset, count ) );
	}

	/// <summary>Read an int from the buffer.</summary>
	internal int ReadInt( long offset )
	{
		ObjectDisposedException.ThrowIf( _disposed, this );
		var span = GetSpan( offset, 4 );
		return span[ 0 ] << 24 |
			   span[ 1 ] << 16 |
			   span[ 2 ] << 8 |
			   span[ 3 ];
	}

	/// <summary>Read a variable-sized int from the buffer.</summary>
	internal int ReadVarInt( long offset, int count )
	{
		ObjectDisposedException.ThrowIf( _disposed, this );
		if( count == 0 )
			return 0;
		if( count == 4 )
			return ReadInt( offset );
		var span = GetSpan( offset, count );
		return count switch
		{
			1 => span[ 0 ],
			2 => span[ 0 ] << 8 |
				 span[ 1 ],
			3 => span[ 0 ] << 16 |
				 span[ 1 ] << 8 |
				 span[ 2 ],
			_ => throw new InvalidDatabaseException( $"Unexpected int32 of size {count}" ),
		};
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	internal int HashBytes( long offset, int count )
	{
		var code = 17;
		var span = GetSpan( offset, count );
		for( var i = 0; i < span.Length; i++ )
			code = unchecked(( 31 * code ) + span[ i ]);
		return code;
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	internal bool EqualsBytes( long offset, MemoryMapBuffer other, long otherOffset, int count )
	{
		return GetSpan( offset, count ).SequenceEqual( other.GetSpan( otherOffset, count ) );
	}

	[MethodImpl( MethodImplOptions.AggressiveInlining )]
	internal bool EqualsBytes( long offset, byte[] other, int otherOffset, int count )
	{
		return GetSpan( offset, count ).SequenceEqual( other.AsSpan( otherOffset, count ) );
	}

	/// <summary>Read a big integer from the buffer.</summary>
	internal BigInteger ReadBigInteger( long offset, int size )
	{
		var buffer = Read( offset, size );
		Array.Reverse( buffer );
		if( buffer.Length > 0 && ( buffer[ buffer.Length - 1 ] & 0x80 ) > 0 )
			Array.Resize( ref buffer, buffer.Length + 1 );
		return new BigInteger( buffer );
	}

	/// <summary>Read a double from the buffer.</summary>
	internal double ReadDouble( long offset ) => BitConverter.Int64BitsToDouble( ReadLong( offset, 8 ) );

	/// <summary>Read a float from the buffer.</summary>
	internal float ReadFloat( long offset ) => BitConverter.Int32BitsToSingle( ReadInt( offset ) );

	/// <summary>
	///     Read a long from the buffer.
	/// </summary>
	internal long ReadLong( long offset, int size )
	{
		ObjectDisposedException.ThrowIf( _disposed, this );
		var span = GetSpan( offset, size );
		long val = 0;
		for( var i = 0; i < span.Length; i++ )
			val = ( val << 8 ) | span[ i ];
		return val;
	}

	/// <summary>Read a uint64 from the buffer.</summary>
	internal ulong ReadULong( long offset, int size )
	{
		ObjectDisposedException.ThrowIf( _disposed, this );

		var span = GetSpan( offset, size );
		ulong val = 0;
		for( var i = 0; i < span.Length; i++ )
			val = ( val << 8 ) | span[ i ];
		return val;
	}

	public void Dispose()
	{
		Dispose( true );
		GC.SuppressFinalize( this );
	}

	/// <summary>
	///     Release resources back to the system.
	/// </summary>
	/// <param name="disposing"></param>
	private void Dispose( bool disposing )
	{
		if( _disposed )
			return;

		if( disposing )
		{
			try
			{
				_view?.SafeMemoryMappedViewHandle.ReleasePointer();
			}
			finally
			{
				_view?.Dispose();
				_memoryMappedFile.Dispose();
			}
		}

		_disposed = true;
	}
}