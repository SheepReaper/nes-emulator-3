namespace Sheep.Emulation.Nes.Debugging;

internal static class CpuOpcodeTable
{
    private static readonly CpuOpcodeDescriptor?[] Entries = Build();

    public static CpuOpcodeDescriptor? Get(byte opcode) => Entries[opcode];
    public static bool IsOfficial(byte opcode) => Entries[opcode]?.IsOfficial == true;

    private static CpuOpcodeDescriptor?[] Build()
    {
        var entries = new CpuOpcodeDescriptor?[256];
        void Add(byte op, string name, CpuAddressingMode mode, int length, int cycles, bool isOfficial) =>
            entries[op] = new CpuOpcodeDescriptor(op, name, mode, length, cycles, isOfficial);

        CpuOfficialOpcodes00To3F.Populate(Add);
        CpuOfficialOpcodes40To7F.Populate(Add);
        CpuOfficialOpcodes80ToBF.Populate(Add);
        CpuOfficialOpcodesC0ToFF.Populate(Add);
        CpuUnofficialOpcodes.Populate(Add);
        return entries;
    }
}