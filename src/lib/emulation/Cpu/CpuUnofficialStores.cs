namespace Sheep.Emulation.Nes.Cpu;

internal static class CpuUnofficialStores
{
    [CpuBehavior(
        CpuBehaviorKind.Nmos6502Quirk,
        "SHY/SHX mask the stored register with base-high + 1 and replace the destination high byte with that value on page crossing.",
        "https://www.nesdev.org/wiki/CPU_unofficial_opcodes")]
    internal static void StoreHighMaskedIndexed(Cpu cpu, CpuState s, byte index, byte registerValue)
    {
        var baseAddress = CpuStackOperations.ReadWord(cpu, s.ProgramCounter);
        s.ProgramCounter += 2;
        var effectiveAddress = (ushort)(baseAddress + index);
        _ = cpu.Read((ushort)((baseAddress & 0xFF00) | (effectiveAddress & 0x00FF)));
        cpu.CompleteWrite(() =>
        {
            var value = (byte)(registerValue & (s.DmaHaltOccurred ? 0xFF : (((baseAddress >> 8) + 1) & 0xFF)));
            var targetAddress = effectiveAddress;
            if ((baseAddress & 0xFF) + index > 0xFF)
            {
                targetAddress = (ushort)((value << 8) | (effectiveAddress & 0xFF));
            }
            cpu.Write(targetAddress, value);
        });
    }

    internal static void StoreAhx(Cpu cpu, CpuState s, ushort baseAddress, byte index, bool updateStackPointer)
    {
        var registerValue = (byte)(s.A & s.X);
        if (updateStackPointer)
        {
            s.SP = registerValue;
        }
        var effectiveAddress = (ushort)(baseAddress + index);
        _ = cpu.Read((ushort)((baseAddress & 0xFF00) | (effectiveAddress & 0x00FF)));
        cpu.CompleteWrite(() =>
        {
            var value = (byte)(registerValue & (s.DmaHaltOccurred ? 0xFF : (((baseAddress >> 8) + 1) & 0xFF)));
            var targetAddress = effectiveAddress;
            if ((baseAddress & 0xFF) + index > 0xFF)
            {
                targetAddress = (ushort)((value << 8) | (effectiveAddress & 0xFF));
            }
            cpu.Write(targetAddress, value);
        });
    }
}
