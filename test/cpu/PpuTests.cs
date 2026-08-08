using SR.Emulation.Nes;
using SR.Emulation.Nes.Abtractions;
using System.Reflection;
using Xunit;

namespace SR.Emulation.Nes.Tests;

public sealed class PpuTests
{
    [Fact]
    public void RegisterBitfieldsReflectTheirBackingByte()
    {
        var control = new PpuCtrl { Value = 0xBF };
        Assert.True(control.VramIncrement);
        Assert.True(control.SpritePatternTableAddress);
        Assert.True(control.BackgroundPatternTableAddress);
        Assert.True(control.SpriteSize);
        Assert.False(control.PpuMasterSlaveSelect);
        Assert.True(control.VBlankNmiEnable);
        Assert.Equal(0x2C00, control.BaseNametableAddress);

        var mask = new PpuMask { Value = 0xFF };
        Assert.True(mask.Grayscale);
        Assert.True(mask.ShowBackgroundLeft);
        Assert.True(mask.ShowSpritesLeft);
        Assert.True(mask.ShowBackground);
        Assert.True(mask.ShowSprites);
        Assert.True(mask.EmphasizeRed);
        Assert.True(mask.EmphasizeGreen);
        Assert.True(mask.EmphasizeBlue);

        var status = new PpuStatus { Value = 0xE0 };
        Assert.True(status.SpriteOverflow);
        Assert.True(status.Sprite0Hit);
        Assert.True(status.VBlank);
        status.Sprite0Hit = false;
        Assert.Equal(0xA0, status.Value);
    }

    [Fact]
    public void NesExposesPortableFrameDescription()
    {
        var nes = new Nes(NesVideoStandard.Pal);

        Assert.Equal(NesVideoStandard.Pal, nes.VideoStandard);
        Assert.Equal(256, Nes.FrameWidth);
        Assert.Equal(240, Nes.FrameHeight);
        Assert.Equal(4, Nes.BytesPerPixel);
        Assert.Equal(256 * 240 * 4, Nes.FrameBufferSize);
    }

    [Fact]
    public void TryCopyFrameReturnsFalseUntilAFrameIsPublished()
    {
        var nes = new Nes();
        var pixels = new byte[Nes.FrameBufferSize];

        Assert.False(nes.TryCopyFrame(pixels, out var frameNumber));
        Assert.Equal(0UL, frameNumber);
    }

    [Fact]
    public void TryCopyFrameRejectsAnUndersizedDestination()
    {
        var nes = new Nes();

        Assert.Throws<ArgumentException>(() => nes.TryCopyFrame(new byte[Nes.FrameBufferSize - 1], out _));
    }

    [Theory]
    [InlineData(NesVideoStandard.Ntsc, 16UL)]
    [InlineData(NesVideoStandard.Pal, 15UL)]
    public void VideoStandardUsesTheCorrectCpuToPpuClockRatio(NesVideoStandard standard, ulong expectedCpuClocks)
    {
        var nes = new Nes(standard);
        for (var i = 0; i < 48; i++) nes.Clock();

        var field = typeof(Nes).GetField("_cpuClockCounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(expectedCpuClocks, (ulong)field.GetValue(nes)!);
    }

    [Theory]
    [InlineData(NesVideoStandard.Ntsc, 341 * 262)]
    [InlineData(NesVideoStandard.Pal, 341 * 312)]
    public void CompletedFrameRaisesNotificationAndCanBeCopied(NesVideoStandard standard, int maximumClocks)
    {
        var nes = new Nes(standard);
        FrameReadyEventArgs? notification = null;
        nes.FrameReady += (_, args) => notification = args;

        for (var i = 0; i < maximumClocks && notification is null; i++) nes.Clock();

        Assert.NotNull(notification);
        Assert.Equal(1UL, notification.FrameNumber);
        Assert.Equal(standard, notification.VideoStandard);

        var pixels = new byte[Nes.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out var copiedFrame));
        Assert.Equal(notification.FrameNumber, copiedFrame);
        for (var i = 3; i < pixels.Length; i += 4) Assert.Equal(0xFF, pixels[i]);
    }

    [Fact]
    public void FrameReadySubscriberExceptionsPropagateFromClock()
    {
        var nes = new Nes();
        EventHandler<FrameReadyEventArgs> handler = (_, _) => throw new InvalidOperationException("consumer failed");
        nes.FrameReady += handler;

        Assert.Throws<InvalidOperationException>(() =>
        {
            for (var i = 0; i < 341 * 262; i++) nes.Clock();
        });
        nes.FrameReady -= handler;
        nes.Clock();
        Assert.True(nes.TryCopyFrame(new byte[Nes.FrameBufferSize], out var frameNumber));
        Assert.Equal(1UL, frameNumber);
    }

    [Fact]
    public void MapperZeroBackgroundRendersThroughThePublicFrameApi()
    {
        var nes = new Nes();
        nes.LoadRom(CreateSolidBackgroundRom());
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;

        for (var i = 0; i < 341 * 262 && !completed; i++) nes.Clock();

        var pixels = new byte[Nes.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        var offset = ((20 * Nes.FrameWidth) + 20) * Nes.BytesPerPixel;
        Assert.Equal(236, pixels[offset]);
        Assert.Equal(238, pixels[offset + 1]);
        Assert.Equal(236, pixels[offset + 2]);
        Assert.Equal(255, pixels[offset + 3]);
    }

    [Fact]
    public void OamDmaSpriteRendersThroughThePublicFrameApi()
    {
        var nes = new Nes();
        nes.LoadRom(CreateSpriteRom());
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;

        for (var i = 0; i < 341 * 262 && !completed; i++) nes.Clock();

        var pixels = new byte[Nes.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        var offset = ((32 * Nes.FrameWidth) + 42) * Nes.BytesPerPixel;
        Assert.Equal(152, pixels[offset]);
        Assert.Equal(34, pixels[offset + 1]);
        Assert.Equal(32, pixels[offset + 2]);
    }

    [Theory]
    [InlineData(0x0B, 0x16, 152, 150, 152)] // Grayscale maps $16 to luminance column $10.
    [InlineData(0x2A, 0x30, 236, 178, 177)] // Red emphasis attenuates green and blue.
    public void PpuMaskColorEffectsAreAppliedToRgbaOutput(
        byte mask, byte paletteColor, byte red, byte green, byte blue)
    {
        var nes = new Nes();
        nes.LoadRom(CreateSolidBackgroundRom(mask, paletteColor));
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;
        for (var i = 0; i < 341 * 262 && !completed; i++) nes.Clock();

        var pixels = new byte[Nes.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        var offset = ((20 * Nes.FrameWidth) + 20) * Nes.BytesPerPixel;
        Assert.Equal(red, pixels[offset]);
        Assert.Equal(green, pixels[offset + 1]);
        Assert.Equal(blue, pixels[offset + 2]);
    }

    [Fact]
    public void PalUsesItsOwnPaletteAndBlackPictureBorder()
    {
        var nes = new Nes(NesVideoStandard.Pal);
        nes.LoadRom(CreateSolidBackgroundRom());
        var completed = false;
        nes.FrameReady += (_, _) => completed = true;
        while (!completed) nes.Clock();

        var pixels = new byte[Nes.FrameBufferSize];
        Assert.True(nes.TryCopyFrame(pixels, out _));
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, pixels.AsSpan(0, 4).ToArray());
        var content = ((20 * Nes.FrameWidth) + 20) * 4;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, pixels.AsSpan(content, 4).ToArray());
    }

    [Fact]
    public void NtscOddRenderedFrameSkipsOnePpuClock()
    {
        var nes = new Nes();
        nes.LoadRom(CreateSolidBackgroundRom());
        var frames = 0;
        nes.FrameReady += (_, _) => frames++;
        while (frames < 1) nes.Clock();

        var clocks = 0;
        while (frames < 3)
        {
            nes.Clock();
            clocks++;
        }

        Assert.Equal((341 * 262 * 2) - 1, clocks);
    }

    [Fact]
    public void StatusReadReturnsOpenBusBitsAndResetsTheSharedWriteLatch()
    {
        var ppu = CreatePpu(out var bus);
        ppu.Write(0x2000, 0x1F);

        Assert.Equal(0x1F, ppu.Read(0x2002));

        ppu.Write(0x2005, 0x2B);
        _ = ppu.Read(0x2002);
        ppu.Write(0x2006, 0x21);
        ppu.Write(0x2006, 0x05);
        ppu.Write(0x2007, 0x77);
        Assert.Equal(0x77, bus.Memory[0x2105]);
    }

    [Fact]
    public void StatusReadImmediatelyBeforeVblankSuppressesThatFramesFlag()
    {
        var ppu = CreatePpu(out _);
        for (var i = 0; i < 241 * 341; i++) ppu.Clock();

        Assert.Equal(0, ppu.Read(0x2002) & 0x80);
        ppu.Clock();
        ppu.Clock();
        Assert.Equal(0, ppu.Read(0x2002) & 0x80);
    }

    [Fact]
    public void EnablingNmiDuringVblankRaisesNmiAndStatusReadClearsIt()
    {
        var interrupts = new InterruptLines();
        var ppu = new Ppu(interrupts);
        ppu.ConnectBus(new MemoryBus());
        ppu.Reset();
        for (var i = 0; i < (241 * 341) + 2; i++) ppu.Clock();

        ppu.Write(0x2000, 0x80);
        Assert.True(interrupts.Nmi);
        Assert.NotEqual(0, ppu.Read(0x2002) & 0x80);
        Assert.False(interrupts.Nmi);
    }

    [Fact]
    public void PpuDataImplementsBufferedAndImmediatePaletteReads()
    {
        var ppu = CreatePpu(out var bus);
        bus.Memory[0x2000] = 0xAB;
        SetPpuAddress(ppu, 0x2000);
        Assert.Equal(0, ppu.Read(0x2007));
        Assert.Equal(0xAB, ppu.Read(0x2007));

        bus.Memory[0x3F00] = 0xCD;
        bus.Memory[0x2F00] = 0xEF;
        SetPpuAddress(ppu, 0x3F00);
        Assert.Equal(0xCD, ppu.Read(0x2007));
        SetPpuAddress(ppu, 0x2000);
        Assert.Equal(0xEF, ppu.Read(0x2007));
    }

    [Fact]
    public void PpuDataHonorsThirtyTwoByteIncrementMode()
    {
        var ppu = CreatePpu(out var bus);
        ppu.Write(0x2000, 0x04);
        SetPpuAddress(ppu, 0x2100);
        ppu.Write(0x2007, 0x11);
        ppu.Write(0x2007, 0x22);

        Assert.Equal(0x11, bus.Memory[0x2100]);
        Assert.Equal(0x22, bus.Memory[0x2120]);
    }

    [Fact]
    public void MoreThanEightSpritesOnTheNextScanlineSetsOverflow()
    {
        var ppu = CreatePpu(out _);
        FillOam(ppu, 0xFF);
        ppu.Write(0x2003, 0);
        for (var sprite = 0; sprite < 9; sprite++)
        {
            ppu.Write(0x2004, 9);
            ppu.Write(0x2004, 0);
            ppu.Write(0x2004, 0);
            ppu.Write(0x2004, (byte)(sprite * 8));
        }
        ppu.Write(0x2001, 0x18);

        for (var i = 0; i < (9 * 341) + 258; i++) ppu.Clock();

        Assert.NotEqual(0, ppu.Read(0x2002) & 0x20);
    }

    [Fact]
    public void OpaqueSpriteZeroOverOpaqueBackgroundSetsSpriteZeroHit()
    {
        var ppu = CreatePpu(out var bus);
        for (var row = 0; row < 8; row++) bus.Memory[row] = 0xFF;
        FillOam(ppu, 0xFF);
        ppu.Write(0x2003, 0);
        ppu.Write(0x2004, 9);
        ppu.Write(0x2004, 0);
        ppu.Write(0x2004, 0);
        ppu.Write(0x2004, 20);
        ppu.Write(0x2001, 0x1E);

        for (var i = 0; i < (11 * 341); i++) ppu.Clock();

        Assert.NotEqual(0, ppu.Read(0x2002) & 0x40);
    }

    private static byte[] CreateSolidBackgroundRom(byte mask = 0x0A, byte paletteColor = 0x30)
    {
        var rom = new byte[16 + 0x4000 + 0x2000];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;

        var program = new byte[]
        {
            0xA9, 0x3F, 0x8D, 0x06, 0x20, // LDA #$3F; STA $2006
            0xA9, 0x00, 0x8D, 0x06, 0x20, // LDA #$00; STA $2006
            0xA9, 0x0F, 0x8D, 0x07, 0x20, // backdrop = black
            0xA9, paletteColor, 0x8D, 0x07, 0x20,
            0xA9, mask, 0x8D, 0x01, 0x20,
            0x4C, 0x19, 0x80              // JMP $8019
        };
        program.CopyTo(rom, 16);
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        for (var row = 0; row < 8; row++) rom[16 + 0x4000 + row] = 0xFF;
        return rom;
    }

    private static byte[] CreateSpriteRom()
    {
        var rom = new byte[16 + 0x4000 + 0x2000];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = 1; rom[5] = 1;
        var program = new byte[]
        {
            0xA9, 0x3F, 0x8D, 0x06, 0x20,
            0xA9, 0x11, 0x8D, 0x06, 0x20,
            0xA9, 0x16, 0x8D, 0x07, 0x20,
            0xA2, 0x00,             // LDX #0
            0xA9, 0xFF,             // LDA #$FF
            0x9D, 0x00, 0x02,       // loop: STA $0200,X
            0xE8,                   // INX
            0xD0, 0xFA,             // BNE loop
            0xA9, 29, 0x8D, 0x00, 0x02,
            0xA9, 1, 0x8D, 0x01, 0x02,
            0xA9, 0, 0x8D, 0x02, 0x02,
            0xA9, 40, 0x8D, 0x03, 0x02,
            0xA9, 2, 0x8D, 0x14, 0x40,
            0xA9, 0x14, 0x8D, 0x01, 0x20,
            0x4C, 0x39, 0x80
        };
        program.CopyTo(rom, 16);
        rom[16 + 0x3FFC] = 0x00;
        rom[16 + 0x3FFD] = 0x80;
        for (var row = 0; row < 8; row++) rom[16 + 0x4000 + 16 + row] = 0xFF;
        return rom;
    }

    private static Ppu CreatePpu(out MemoryBus bus)
    {
        var ppu = new Ppu(new InterruptLines());
        bus = new MemoryBus();
        ppu.ConnectBus(bus);
        ppu.Reset();
        return ppu;
    }

    private static void FillOam(Ppu ppu, byte value)
    {
        ppu.Write(0x2003, 0);
        for (var i = 0; i < 256; i++) ppu.Write(0x2004, value);
    }

    private static void SetPpuAddress(Ppu ppu, ushort address)
    {
        _ = ppu.Read(0x2002);
        ppu.Write(0x2006, (byte)(address >> 8));
        ppu.Write(0x2006, (byte)address);
    }

    private sealed class MemoryBus : IBus
    {
        public byte[] Memory { get; } = new byte[0x4000];
        public byte Read(ushort address) => Memory[address & 0x3FFF];
        public void Write(ushort address, byte value) => Memory[address & 0x3FFF] = value;
    }
}
