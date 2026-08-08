using Xunit;

namespace SR.Emulation.Nes.ConformanceTests;

public sealed class BlarggProtocolTests
{
    [Fact]
    public void Read_ReturnsNoResultWithoutTheBlarggSignature()
    {
        var memory = new byte[0x10000];
        memory[0x6000] = 0;

        Assert.Null(BlarggProtocol.Read(address => memory[address]));
    }

    [Fact]
    public void Read_ReturnsRunningResetAndCompletedStatesWithDiagnosticText()
    {
        var memory = new byte[0x10000];
        memory[0x6001] = 0xDE;
        memory[0x6002] = 0xB0;
        memory[0x6003] = 0x61;
        "passed\0"u8.CopyTo(memory.AsSpan(0x6004));

        memory[0x6000] = 0x80;
        Assert.Equal(BlarggTestState.Running, BlarggProtocol.Read(address => memory[address])!.State);
        memory[0x6000] = 0x81;
        Assert.Equal(BlarggTestState.ResetRequested, BlarggProtocol.Read(address => memory[address])!.State);
        memory[0x6000] = 0;
        var result = BlarggProtocol.Read(address => memory[address])!;
        Assert.Equal(BlarggTestState.Completed, result.State);
        Assert.Equal(0, result.Code);
        Assert.Equal("passed", result.Output);
    }

    [Fact]
    public void Read_LimitsUnterminatedDiagnosticText()
    {
        var memory = new byte[0x10000];
        memory[0x6001] = 0xDE;
        memory[0x6002] = 0xB0;
        memory[0x6003] = 0x61;
        memory.AsSpan(0x6004).Fill((byte)'A');

        var result = BlarggProtocol.Read(address => memory[address], maximumTextLength: 8)!;

        Assert.Equal("AAAAAAAA", result.Output);
    }
}
