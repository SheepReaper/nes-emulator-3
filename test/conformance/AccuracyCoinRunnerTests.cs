using Xunit;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed class AccuracyCoinRunnerTests
{
    [Fact]
    public void Run_PressesStartAndReadsTheCompletedAggregateResult()
    {
        var machine = new FakeAccuracyCoinMachine();

        var result = new AccuracyCoinRunner(machine, chunkSize: 10).Run(100);

        Assert.Equal(AccuracyCoinOutcome.Passed, result.Outcome);
        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Passed);
        Assert.Equal(1, result.Skipped);
        Assert.Equal([NesControllerButton.Start, NesControllerButton.None], machine.ControllerStates);
    }

    [Fact]
    public void Run_ReportsNonPassingResultBytes()
    {
        var machine = new FakeAccuracyCoinMachine(fail: true);

        var result = new AccuracyCoinRunner(machine, chunkSize: 10).Run(100);

        Assert.Equal(AccuracyCoinOutcome.Failed, result.Outcome);
        Assert.Contains(result.NonPassingResults, item => item.Address == 0x0401 && item.Value == 0x02);
    }

    [Fact]
    public void Run_TimesOutWhenTheSuiteNeverStarts()
    {
        var machine = new FakeAccuracyCoinMachine(neverStart: true);

        var result = new AccuracyCoinRunner(machine, chunkSize: 10).Run(30);

        Assert.Equal(AccuracyCoinOutcome.TimedOut, result.Outcome);
        Assert.Equal(30, result.ElapsedPpuDots);
    }

    private sealed class FakeAccuracyCoinMachine(bool fail = false, bool neverStart = false) : INesTestMachine
    {
        private int _runs;
        private NesControllerButton _buttons;
        public List<NesControllerButton> ControllerStates { get; } = [];
        public ushort ProgramCounter => 0;

        public void RunForPpuDots(int count) => _runs++;
        public void Reset() { }

        public void SetControllerState(int controller, NesControllerButton buttons)
        {
            Assert.Equal(0, controller);
            _buttons = buttons;
            ControllerStates.Add(buttons);
        }

        public byte PeekCpuMemory(ushort address)
        {
            return neverStart
                ? (byte)0
                : (byte)(address switch
            {
                0x0035 => _runs is 2 or 3 ? (byte)1 : (byte)0,
                0x0037 => _runs >= 4 ? (byte)3 : (byte)0,
                0x0038 => _runs >= 4 ? (byte)(fail ? 1 : 2) : (byte)0,
                0x003F => _runs >= 4 ? (byte)1 : (byte)0,
                0x0700 => _runs >= 4 ? (byte)0x4C : (byte)0,
                0x0401 when fail => 0x02,
                0x0402 => 0xFF,
                _ => 0x01
            });
        }

        public byte PeekPpuMemory(ushort address) => 0;
        public void WriteCpuMemory(ushort address, byte value) { }
        public void SetCpuRegisters(Sheep.Emulation.Nes.Debugging.CpuRegisterValues registers) { }
    }
}
