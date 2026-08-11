using NesCpu = Sheep.Emulation.Nes.Cpu.Cpu;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuMockBus : IBus
{
    private readonly byte[] _memory = new byte[0x10000];

    public byte Read(ushort address) => _memory[address];
    public void Write(ushort address, byte value) => _memory[address] = value;

    public void Load(ushort address, byte[] data)
    {
        Array.Copy(data, 0, _memory, address, data.Length);
    }
}

public abstract class CpuTestFixture
{
    protected readonly InterruptLines Interrupts = new();
    protected readonly CpuMockBus Bus = new();
    protected readonly NesCpu Cpu;

    protected CpuTestFixture()
    {
        Cpu = new NesCpu(Interrupts);
        Cpu.ConnectBus(Bus);
    }

    protected void Clock(int cycles)
    {
        for (var i = 0; i < cycles; i++)
        {
            Cpu.Clock();
        }
    }

    protected ushort GetPc() => Cpu.State.ProgramCounter;
    protected void SetPc(ushort value) => Cpu.State.ProgramCounter = value;
    protected byte GetA() => Cpu.State.A;
    protected void SetA(byte value) => Cpu.State.A = value;
    protected byte GetX() => Cpu.State.X;
    protected byte GetY() => Cpu.State.Y;
    protected void SetX(byte value) => Cpu.State.X = value;
    protected void SetY(byte value) => Cpu.State.Y = value;
    protected byte GetSp() => Cpu.State.SP;
    protected void SetSp(byte value) => Cpu.State.SP = value;
    protected byte GetP() => Cpu.State.P.Value;
    protected void SetP(byte value) => Cpu.State.P = new ProcessorStatus { Value = value };
    protected int GetCycles() => Cpu.State.Cycles;
    protected void SetCycles(byte value) => Cpu.State.Cycles = value;

    protected bool GetFlag(char flag)
    {
        var p = Cpu.State.P;
        return flag switch
        {
            'N' => p.Negative,
            'Z' => p.Zero,
            'C' => p.Carry,
            'V' => p.Overflow,
            'I' => p.InterruptDisable,
            'D' => p.Decimal,
            _ => throw new ArgumentException("Invalid flag specified.")
        };
    }

    protected void SetFlag(char flag, bool value)
    {
        var p = Cpu.State.P;
        switch (flag)
        {
            case 'N': p.Negative = value; break;
            case 'Z': p.Zero = value; break;
            case 'C': p.Carry = value; break;
            case 'V': p.Overflow = value; break;
            case 'I': p.InterruptDisable = value; break;
            case 'D': p.Decimal = value; break;
            default: throw new ArgumentException("Invalid flag specified.");
        }
        Cpu.State.P = p;
    }
}
