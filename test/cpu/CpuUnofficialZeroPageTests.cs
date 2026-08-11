using System.Reflection;

using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialZeroPageTests : CpuTestFixture
{
    [Fact]
    public void CpuVariantBehaviorAnnotations_DistinguishNesDeviationFromInheritedNmosQuirk()
    {
        var atx = typeof(CpuInstructionsUnofficial).GetMethod("AtxImm", BindingFlags.Static | BindingFlags.NonPublic)!;
        var readWordBug = typeof(CpuStackOperations).GetMethod("ReadWordBug", BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.Equal(
            CpuBehaviorKind.Nes2A03Deviation,
            atx.GetCustomAttribute<CpuBehaviorAttribute>()!.Kind);
        Assert.Equal(
            CpuBehaviorKind.Nmos6502Quirk,
            readWordBug.GetCustomAttribute<CpuBehaviorAttribute>()!.Kind);
    }

    [Theory]
    [InlineData(0x0A, 0x04, true, false)]
    [InlineData(0x10, 0xFE, false, true)]
    public void AxsImmediate_SubtractsFromAccumulatorAndXIntersectionWithoutBorrow(
        byte operand, byte expectedResult, bool expectedCarry, bool expectedNegative)
    {
        Bus.Load(0x8000, [0xCB, operand]);
        SetPc(0x8000);
        SetA(0x0F);
        SetX(0x0E);
        SetCycles(0);

        Clock(2);

        Assert.Equal(expectedResult, GetX());
        Assert.Equal(expectedCarry, GetFlag('C'));
        Assert.Equal(expectedNegative, GetFlag('N'));
        Assert.False(GetFlag('Z'));
    }

    [Fact]
    public void SxaAbsoluteY_StoresHighAddressMaskedAndCrossPageReplacesHighByte()
    {
        Bus.Load(0x8000, [0x9E, 0x50, 0x20]);
        SetPc(0x8000);
        SetX(0x42);
        SetY(0x10);
        SetCycles(0);

        Clock(5);

        Assert.Equal(0x42 & 0x21, Bus.Read(0x2060));
    }

    [Fact]
    public void SyaAbsoluteX_StoresHighAddressMaskedAndCrossPageReplacesHighByte()
    {
        Bus.Load(0x8000, [0x9C, 0xF0, 0x20]);
        SetPc(0x8000);
        SetX(0x20);
        SetY(0x25);
        SetCycles(0);

        Clock(5);

        Assert.Equal(0x21, Bus.Read((ushort)((0x21 << 8) | 0x10)));
    }
}
