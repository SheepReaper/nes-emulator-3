namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuDmaReader
{
    internal static byte ReadDmaSource(
        ushort address,
        byte[] ram,
        Ppu ppu,
        Apu apu,
        CartridgeSlot cartridgeSlot,
        CpuControllerPorts controllers,
        ref byte openBus,
        byte internalBus)
    {
        var internalReg = (ushort)(0x4000 | (address & 0x001F));
        var isExternalDriven = address <= 0x1FFF || (cartridgeSlot.Cartridge?.CpuReadDrivesDataBus(address) ?? false);
        var externalValue = address switch
        {
            <= 0x1FFF => ram[address & 0x07FF],
            <= 0x3FFF => ppu.Read(address),
            <= 0x401F => openBus,
            _ => cartridgeSlot.CpuReadOrOpenBus(address, openBus)
        };

        byte internalValue;
        bool internalDrives;
        switch (internalReg)
        {
            case 0x4015:
                internalValue = (byte)(apu.Read(internalReg) | (internalBus & 0x20));
                internalDrives = false;
                break;
            case 0x4016:
                internalValue = controllers.ReadController(0, openBus);
                internalDrives = true;
                break;
            case 0x4017:
                internalValue = controllers.ReadController(1, openBus);
                internalDrives = true;
                break;
            default:
                internalValue = openBus;
                internalDrives = false;
                break;
        }

        byte result = internalDrives && isExternalDriven
            ? (byte)((internalValue & 0x1F) | (externalValue & 0xE0))
            : internalDrives
                ? internalValue
                : externalValue;

        openBus = result;
        return result;
    }

    internal static byte ReadOamDmaSource(
        ushort address,
        byte[] ram,
        Ppu ppu,
        Apu apu,
        CartridgeSlot cartridgeSlot,
        CpuControllerPorts controllers,
        ref byte openBus,
        bool hasCpuReadAddress,
        ushort lastCpuReadAddress)
    {
        var isExternalDriven = address <= 0x1FFF || (cartridgeSlot.Cartridge?.CpuReadDrivesDataBus(address) ?? false);
        var externalValue = address switch
        {
            <= 0x1FFF => ram[address & 0x07FF],
            <= 0x3FFF => ppu.Read(address),
            <= 0x401F => openBus,
            _ => cartridgeSlot.CpuReadOrOpenBus(address, openBus)
        };

        if (hasCpuReadAddress && lastCpuReadAddress is >= 0x4000 and <= 0x401F)
        {
            var internalAddress = (ushort)(0x4000 | (address & 0x001F));
            if (internalAddress == 0x4015)
            {
                var bit5 = (byte)((isExternalDriven ? externalValue : openBus) & 0x20);
                externalValue = (byte)(apu.Read(0x4015) | bit5);
            }
            else if (!isExternalDriven && internalAddress is 0x4016 or 0x4017)
            {
                externalValue = internalAddress == 0x4016
                    ? controllers.ReadController(0, openBus)
                    : controllers.ReadController(1, openBus);
            }
        }

        openBus = externalValue;
        return externalValue;
    }
}
