using Xunit;

namespace EmuSheep.Tests;

public sealed class RgbaToBgraConverterTests
{
    [Fact]
    public void Convert_SwapsRedAndBlueAndPreservesGreenAndAlpha()
    {
        byte[] source = [0x11, 0x22, 0x33, 0x44];
        byte[] destination = new byte[source.Length];

        RgbaToBgraConverter.Convert(source, destination);

        Assert.Equal([0x33, 0x22, 0x11, 0x44], destination);
    }

    [Fact]
    public void Convert_ConvertsEveryPixel()
    {
        byte[] source = [0x01, 0x02, 0x03, 0xFF, 0x10, 0x20, 0x30, 0x80];
        byte[] destination = new byte[source.Length];

        RgbaToBgraConverter.Convert(source, destination);

        Assert.Equal([0x03, 0x02, 0x01, 0xFF, 0x30, 0x20, 0x10, 0x80], destination);
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    public void Convert_RejectsBuffersThatCannotDescribeMatchingWholePixels(int sourceLength, int destinationLength)
    {
        byte[] source = new byte[sourceLength];
        byte[] destination = new byte[destinationLength];

        Assert.Throws<ArgumentException>(() => RgbaToBgraConverter.Convert(source, destination));
    }
}
