namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Mock test machine for unit testing NesTestRomRunner.
/// </summary>
internal sealed class FakeTestMachine(params byte[] statuses) : INesTestMachine
{
    private readonly Queue<byte> _statuses = new(statuses);
    private byte _status = 0x80;
    public int ResetCount { get; private set; }
    public string Output { get; init; } = "";
    public byte LegacyResult { get; init; }
    public ushort ProgramCounter { get; set; }
    public string ScreenText { get; init; } = "";

    public void RunForPpuDots(int count)
    {
        if (_statuses.Count > 0)
        {
            _status = _statuses.Dequeue();
        }
    }

    public void Reset() => ResetCount++;

    public void SetControllerState(int controller, NesControllerButton buttons)
    {
    }

    public byte PeekCpuMemory(ushort address) => address switch
    {
        0x00F0 => LegacyResult,
        0x6000 => _status,
        0x6001 => 0xDE,
        0x6002 => 0xB0,
        0x6003 => 0x61,
        >= 0x6004 when address - 0x6004 < Output.Length => (byte)Output[address - 0x6004],
        _ => 0
    };

    public byte PeekPpuMemory(ushort address)
    {
        var index = address - 0x2000;
        return index < ScreenText.Length ? (byte)ScreenText[index] : (byte)0;
    }

    public void WriteCpuMemory(ushort address, byte value) { }
    public void SetCpuRegisters(Sheep.Emulation.Nes.Debugging.CpuRegisterValues registers) { }
}
