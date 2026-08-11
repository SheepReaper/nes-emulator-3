namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Routes writes for triangle, noise, and DMC channels.
/// </summary>
internal static class ApuOtherChannelRouter
{
    internal static void Write(
        ushort address,
        byte value,
        ApuChannels channels)
    {
        switch (address)
        {
            case 0x4008:
                channels.Triangle.WriteControl(value);
                break;
            case 0x400A:
                channels.Triangle.WriteTimerLow(value);
                break;
            case 0x400B:
                channels.Triangle.WriteTimerHigh(value);
                break;
            case 0x400C:
                channels.Noise.WriteControl(value);
                break;
            case 0x400E:
                channels.Noise.WritePeriod(value);
                break;
            case 0x400F:
                channels.Noise.WriteLength(value);
                break;
            case 0x4010:
                channels.Dmc.WriteControl(value);
                break;
            case 0x4011:
                channels.Dmc.WriteOutput(value);
                break;
            case 0x4012:
                channels.Dmc.WriteAddress(value);
                break;
            case 0x4013:
                channels.Dmc.WriteLength(value);
                break;
        }
    }
}
