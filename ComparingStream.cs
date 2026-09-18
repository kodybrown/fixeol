namespace fixeol;

using System;
using System.IO;

/// <summary>
/// Compares generated output with an original file without storing or writing the output.
/// </summary>
internal sealed class ComparingStream( Stream original ) : Stream
{
  private bool _hasChanges;

  public bool HasChanges => _hasChanges || original.Position != original.Length;

  public override bool CanRead => false;
  public override bool CanSeek => false;
  public override bool CanWrite => true;
  public override long Length => throw new NotSupportedException();
  public override long Position {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  public override void Write( byte[] buffer, int offset, int count )
  {
    Write(buffer.AsSpan(offset, count));
  }

  public override void Write( ReadOnlySpan<byte> buffer )
  {
    if (_hasChanges) {
      return;
    }

    Span<byte> originalBytes = stackalloc byte[4096];
    while (!buffer.IsEmpty) {
      var count = Math.Min(buffer.Length, originalBytes.Length);
      var read = original.ReadAtLeast(originalBytes[..count], count, throwOnEndOfStream: false);
      if (read != count || !buffer[..count].SequenceEqual(originalBytes[..count])) {
        _hasChanges = true;
        return;
      }
      buffer = buffer[count..];
    }
  }

  public override void Flush() { }

  public override int Read( byte[] buffer, int offset, int count ) => throw new NotSupportedException();
  public override long Seek( long offset, SeekOrigin origin ) => throw new NotSupportedException();
  public override void SetLength( long value ) => throw new NotSupportedException();

  protected override void Dispose( bool disposing )
  {
    if (disposing) {
      original.Dispose();
    }
    base.Dispose(disposing);
  }
}
