using System.Text;

namespace Sheep.Emulation.Nes.ConformanceTests;

internal enum BlarggTestState
{
    Running,
    ResetRequested,
    Completed
}

internal sealed record BlarggTestResult(BlarggTestState State, byte Code, string Output);

internal static class BlarggProtocol
{
    private static readonly byte[] Signature = [0xDE, 0xB0, 0x61];

    internal static BlarggTestResult? Read(Func<ushort, byte> peek, int maximumTextLength = 4096)
    {
        ArgumentNullException.ThrowIfNull(peek);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTextLength);
        for (var index = 0; index < Signature.Length; index++)
        {
            if (peek((ushort)(0x6001 + index)) != Signature[index]) return null;
        }

        var status = peek(0x6000);
        var state = status switch
        {
            0x80 => BlarggTestState.Running,
            0x81 => BlarggTestState.ResetRequested,
            _ => BlarggTestState.Completed
        };
        var text = new byte[maximumTextLength];
        var length = 0;
        while (length < text.Length)
        {
            var value = peek((ushort)(0x6004 + length));
            if (value == 0) break;
            text[length++] = value;
        }

        return new BlarggTestResult(state, status, Encoding.ASCII.GetString(text, 0, length));
    }
}
