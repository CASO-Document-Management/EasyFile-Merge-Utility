namespace MergeUtility.EasyFileREST;

/// <summary>
/// A stream wrapper that disposes the underlying <see cref="HttpResponseMessage"/>
/// when the stream is disposed, ensuring the HTTP connection is returned to the pool.
/// </summary>
internal sealed class ResponseOwningStream : Stream
{
    private readonly Stream _inner;
    private readonly HttpResponseMessage _response;

    public ResponseOwningStream(Stream inner, HttpResponseMessage response)
    {
        _inner = inner;
        _response = response;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => _inner.ReadAsync(buffer, offset, count, ct);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _inner.ReadAsync(buffer, ct);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        _response.Dispose();
        await base.DisposeAsync();
    }
}
