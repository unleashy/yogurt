using System.IO.Pipelines;
using System.Text;
using Yogurt.Server;

namespace Yogurt.Tests;

public class ProtocolWriterTests
{
    private static ProtocolMessage Message(string s) => new(Encoding.UTF8.GetBytes(s));

    private static async ValueTask<string> ReadAllAsync(PipeReader reader)
    {
        var result = await reader.ReadAtLeastAsync(int.MaxValue);
        return Encoding.UTF8.GetString(result.Buffer);
    }

    [Test]
    public async Task Empty()
    {
        var pipe = new Pipe();
        var sut = new ProtocolWriter(pipe.Writer);

        await sut.WriteAsync(Message(""));
        await pipe.Writer.CompleteAsync();

        var data = await ReadAllAsync(pipe.Reader);
        Assert.That(data, Is.EqualTo("Content-Length: 0\r\n\r\n"));
    }

    [Test]
    public async Task Simple()
    {
        var pipe = new Pipe();
        var sut = new ProtocolWriter(pipe.Writer);

        await sut.WriteAsync(Message("abcd\nefgh"));
        await pipe.Writer.CompleteAsync();

        var data = await ReadAllAsync(pipe.Reader);
        Assert.That(data, Is.EqualTo("Content-Length: 9\r\n\r\nabcd\nefgh"));
    }

    [Test]
    public async Task Utf8()
    {
        var pipe = new Pipe();
        var sut = new ProtocolWriter(pipe.Writer);

        await sut.WriteAsync(Message("bunny 🐇"));
        await pipe.Writer.CompleteAsync();

        var data = await ReadAllAsync(pipe.Reader);
        Assert.That(data, Is.EqualTo("Content-Length: 10\r\n\r\nbunny 🐇"));
    }

    [Test]
    public async Task VeryLong()
    {
        var pipe = new Pipe();
        var sut = new ProtocolWriter(pipe.Writer);
        var message = string.Join("", Enumerable.Repeat("message", 1234));

        await sut.WriteAsync(Message(message));
        await pipe.Writer.CompleteAsync();

        var data = await ReadAllAsync(pipe.Reader);
        Assert.That(data, Is.EqualTo($"Content-Length: 8638\r\n\r\n{message}"));
    }
}
