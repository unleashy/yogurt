using System.IO.Pipelines;
using System.Text;
using Yogurt.Server;

namespace Yogurt.Tests;

public class ProtocolListenerTests
{
    [Test]
    public async Task Empty()
    {
        var transport = new FakeTransport();
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SingleMessage()
    {
        var transport = new FakeTransport("Content-Length: 4\r\n\r\nabcd");
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([new ProtocolMessage("abcd")]));
    }

    [Test]
    public async Task Utf8Encoding()
    {
        var transport = new FakeTransport("Content-Length: 4\r\n\r\n🐇");
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([new ProtocolMessage("🐇")]));
    }

    [Test]
    public async Task Utf8EncodingSplitChunk()
    {
        var headers = "Content-Length: 4\r\n\r\n"u8.ToArray();
        var first = headers.Concat<byte>([0xF0, 0x9F]).ToArray();
        var second = new byte[] { 0x8D, 0x9E };

        var transport = new FakeTransport([first, second]);
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([new ProtocolMessage("🍞")]));
    }

    [Test]
    public async Task MultiMessage()
    {
        var transport = new FakeTransport(
            "Content-Length: 6\r\n\r\nyogurtContent-Length: 9\r\n\r\nice cream"
        );
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([
            new ProtocolMessage("yogurt"),
            new ProtocolMessage("ice cream"),
        ]));
    }

    [Test]
    public async Task MultiChunk()
    {
        var transport = new FakeTransport(
            "Content-Length: 10\r\n\r\nstrawberry",
            "Content-Length: 6\r\n\r\npapaya"
        );
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([
            new ProtocolMessage("strawberry"),
            new ProtocolMessage("papaya"),
        ]));
    }

    [Test]
    public async Task SplitChunk()
    {
        var transport = new FakeTransport(
            "Content-",
            "Length: 17\r\n\r\nblack f",
            "orest cake"
        );
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([
            new ProtocolMessage("black forest cake"),
        ]));
    }

    [Test]
    public async Task SplitChunkAtHeaderSep()
    {
        var transport = new FakeTransport(
            "Content-Length: 5\r\n",
            "\r\nscone"
        );
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([
            new ProtocolMessage("scone"),
        ]));
    }

    [Test]
    public async Task ZeroLength()
    {
        var transport = new FakeTransport(
            "Content-Length: 0\r\n\r\n"
        );
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([
            new ProtocolMessage(""),
        ]));
    }

    [TestCase("utf-8")]
    [TestCase("utf8")]
    public async Task WithContentType(string charset)
    {
        var transport = new FakeTransport(
            $"Content-Type: application/vscode-jsonrpc; charset={charset}\r\n",
            "Content-Length: 13\r\n",
            "\r\ndoce de leite"
        );
        var sut = new ProtocolListener(transport);

        var result = await sut.Listen().ToArrayAsync();

        Assert.That(result, Is.EqualTo([
            new ProtocolMessage("doce de leite"),
        ]));
    }

    [TestCase("broken", "Invalid message: missing or malformed headers")]
    [TestCase("\r\n\r\n no content-length", "Invalid headers: missing Content-Length")]
    [TestCase("content-length: -1\r\n\r\nfoo", "Invalid Content-Length: expected an integer between 0 and 2^31-1, got '-1'")]
    [TestCase("content-length: 2147483648\r\n\r\nfoo", "Invalid Content-Length: expected an integer between 0 and 2^31-1, got '2147483648'")]
    [TestCase("content-length: 100\r\n\r\nsmall", "Invalid content: expected 100 bytes, got 5")]
    [TestCase("content-length: 3\r\ncontent-length: 3\r\n\r\nfoo", "Invalid headers: duplicate Content-Length header")]
    [TestCase("content-type: text/plain\r\n\r\nfoo", "Invalid Content-Type: expected 'application/vscode-jsonrpc; charset=utf-8', got 'text/plain'")]
    [TestCase("content-type: application/vscode-jsonrpc; charset=utf8\r\ncontent-type: foo\r\n\r\nfoo", "Invalid headers: duplicate Content-Type header")]
    [TestCase("content-length: 5\r\nbunny-header: bunny\r\n\r\nabcde", "Invalid headers: unknown header 'bunny-header: bunny'")]
    [TestCase("content-length:1\r\n\r\nx", "Invalid header: missing ': ' separator")]
    [TestCase("content-length 1\r\n\r\nx", "Invalid header: missing ': ' separator")]
    public async Task InvalidData(string input, string message)
    {
        var transport = new FakeTransport(input);
        var sut = new ProtocolListener(transport);

        await Assert.ThatAsync(
            () => sut.Listen().ToArrayAsync().AsTask(),
            Throws
                .TypeOf<InvalidDataException>().And
                .Message.EqualTo(message)
        );
    }

    [Test]
    public async Task EarlyCancellation()
    {
        var transport = new FakeTransport(
            "Content-Length: 9\r\n\r\ncancelled"
        );
        var sut = new ProtocolListener(transport);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await sut.Listen(cts.Token).ToArrayAsync(cts.Token);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task MidwayCancellation()
    {
        var transport = new FakeTransport(
            "Content-Length: 13\r\n\r\nnot cancelled",
            "Content-Length: 9\r\n\r\ncancelled"
        );
        var sut = new ProtocolListener(transport);
        using var cts = new CancellationTokenSource();

        var e = sut.Listen(cts.Token).GetAsyncEnumerator(cts.Token);

        _ = await e.MoveNextAsync();
        var first = e.Current;

        await cts.CancelAsync();

        var second = await e.MoveNextAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(first, Is.EqualTo(new ProtocolMessage("not cancelled")));
            Assert.That(second, Is.False);
        }
    }
}

internal sealed class FakeTransport : IDuplexPipe
{
    public MemoryStream OutputStream { get; } = new();

    public PipeReader Input { get; }
    public PipeWriter Output { get; }

    public FakeTransport(byte[][] rawChunks)
    {
        Input = PipeReader.Create(new ChunkyStream(rawChunks));
        Output = PipeWriter.Create(OutputStream);
    }

    public FakeTransport(params string[] chunks)
        : this(chunks.Select(Encoding.UTF8.GetBytes).ToArray())
    {}
}

internal sealed class ChunkyStream(byte[][] chunks) : Stream
{
    private int _currentChunk;
    private int _currentOffset;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0 || _currentChunk >= chunks.Length) return 0;

        var currentChunk = chunks[_currentChunk];
        var length = Math.Min(currentChunk.Length - _currentOffset, count);

        Array.Copy(currentChunk, _currentOffset, buffer, offset, length);

        _currentOffset += length;
        if (_currentOffset >= currentChunk.Length)
        {
            _currentChunk++;
            _currentOffset = 0;
        }

        return length;
    }

    public override void Flush()
    {
        /* no-op */
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Length => throw new NotSupportedException();
    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}
