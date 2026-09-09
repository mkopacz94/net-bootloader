using NetBootloader.Core.Communication;

namespace NetBootloader.Core.Tests;

/// <summary>
/// Test double that serves pre-scripted response bytes and records everything
/// written, so a real captured TX/RX byte sequence can be replayed against
/// <see cref="BootloaderClient"/> without a real device.
/// </summary>
public sealed class FakeBootloaderConnection : IBootloaderConnection
{
    private readonly Queue<byte> _rxQueue;

    public List<byte[]> WrittenPackets { get; } = new();

    public FakeBootloaderConnection(params byte[][] rxChunks)
    {
        _rxQueue = new Queue<byte>(rxChunks.SelectMany(c => c));
    }

    public byte[] Read(int count)
    {
        if (_rxQueue.Count < count)
        {
            throw new InvalidOperationException(
                $"Test setup error: asked for {count} bytes but only {_rxQueue.Count} are queued.");
        }

        var buffer = new byte[count];

        for (var i = 0; i < count; i++)
        {
            buffer[i] = _rxQueue.Dequeue();
        }

        return buffer;
    }

    public int Write(ReadOnlySpan<byte> data)
    {
        WrittenPackets.Add(data.ToArray());
        return data.Length;
    }

    public void DiscardInBuffer()
    {
    }
}
