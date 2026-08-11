using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuTransferAndStatusTests : CpuTestFixture
{
    [Theory]
    [InlineData(0xAA, 'A', 'X', 0x42, false, false, true)]
    [InlineData(0xAA, 'A', 'X', 0x00, true, false, true)]
    [InlineData(0xA8, 'A', 'Y', 0x8F, false, true, true)]
    [InlineData(0x8A, 'X', 'A', 0x42, false, false, true)]
    [InlineData(0x98, 'Y', 'A', 0x42, false, false, true)]
    [InlineData(0xBA, 'S', 'X', 0xFD, false, true, true)]
    [InlineData(0x9A, 'X', 'S', 0x42, false, false, false)]
    public void RegisterTransferInstructions_SetFlagsCorrectly(
        byte opcode, char source, char dest, byte initialValue,
        bool expectedZ, bool expectedN, bool flagsShouldChange)
    {
        Bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetP(0);

        switch (source)
        {
            case 'A': SetA(initialValue); break;
            case 'X': SetX(initialValue); break;
            case 'Y': SetY(initialValue); break;
            case 'S': SetSp(initialValue); break;
        }

        Clock(2);

        byte finalValue = dest switch
        {
            'A' => GetA(),
            'X' => GetX(),
            'Y' => GetY(),
            'S' => GetSp(),
            _ => 0
        };
        Assert.Equal(initialValue, finalValue);

        if (flagsShouldChange)
        {
            Assert.Equal(expectedZ, GetFlag('Z'));
            Assert.Equal(expectedN, GetFlag('N'));
        }
    }

    [Fact]
    public void PHP_PLP_PushAndPullStatus()
    {
        Bus.Load(0x8000, [0x08]);
        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0b10000001);

        Clock(3);

        Assert.Equal(0xFC, GetSp());
        Assert.Equal(0b10110001, Bus.Read(0x01FD));

        Bus.Load(0x8001, [0x28]);
        SetP(0);

        Clock(4);

        Assert.Equal(0xFD, GetSp());
        Assert.Equal(0b10100001, GetP());
    }

    [Fact]
    public void NOP_DoesNothingAndTakesTwoCycles()
    {
        Bus.Load(0x8000, [0xEA]);
        SetPc(0x8000);
        SetA(0xFF);
        SetX(0xFF);
        SetY(0xFF);
        SetSp(0xFF);
        SetP(0xFF);

        Clock(2);

        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetCycles());
        Assert.Equal(0xFF, GetA());
        Assert.Equal(0xFF, GetX());
        Assert.Equal(0xFF, GetY());
        Assert.Equal(0xFF, GetSp());
        Assert.Equal(0xFF, GetP());
    }
}
