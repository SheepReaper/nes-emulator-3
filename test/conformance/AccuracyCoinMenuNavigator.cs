namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Controller navigation helper for AccuracyCoin menu screens.
/// </summary>
internal static class AccuracyCoinMenuNavigator
{
    private const ushort MenuReadyAddress = 0x00EC;
    private const ushort MenuSuiteAddress = 0x0014;
    private const ushort MenuCursorAddress = 0x0016;
    private const ushort NewControllerButtonsAddress = 0x0019;

    internal static bool WaitForMenuReady(
        INesTestMachine machine,
        int chunkSize,
        long maximumPpuDots,
        ref long elapsed)
    {
        while (elapsed < maximumPpuDots && machine.PeekCpuMemory(MenuReadyAddress) != 0x0A)
        {
            RunChunk(machine, chunkSize, NesControllerButton.None, maximumPpuDots, ref elapsed);
        }
        return machine.PeekCpuMemory(MenuReadyAddress) == 0x0A;
    }

    internal static void SelectSuite(
        INesTestMachine machine,
        int chunkSize,
        int suiteIndex,
        long maximumPpuDots,
        ref long elapsed)
    {
        for (var index = 0; index < suiteIndex; index++)
        {
            var previous = machine.PeekCpuMemory(MenuSuiteAddress);
            PulseUntil(machine, chunkSize, NesControllerButton.Right,
                () => machine.PeekCpuMemory(MenuSuiteAddress) != previous,
                maximumPpuDots, ref elapsed);
        }
    }

    internal static void SelectTest(
        INesTestMachine machine,
        int chunkSize,
        int testIndex,
        long maximumPpuDots,
        ref long elapsed)
    {
        for (var index = 0; index <= testIndex; index++)
        {
            var previous = machine.PeekCpuMemory(MenuCursorAddress);
            PulseUntil(machine, chunkSize, NesControllerButton.Down,
                () => machine.PeekCpuMemory(MenuCursorAddress) != previous,
                maximumPpuDots, ref elapsed);
        }
    }

    internal static void PulseUntil(
        INesTestMachine machine,
        int chunkSize,
        NesControllerButton button,
        Func<bool> observed,
        long maximumPpuDots,
        ref long elapsed)
    {
        machine.SetControllerState(0, button);
        while (elapsed < maximumPpuDots && !observed())
        {
            RunChunk(machine, chunkSize, button, maximumPpuDots, ref elapsed, updateController: false);
        }

        machine.SetControllerState(0, NesControllerButton.None);
        while (elapsed < maximumPpuDots && machine.PeekCpuMemory(NewControllerButtonsAddress) != 0)
        {
            RunChunk(machine, chunkSize, NesControllerButton.None, maximumPpuDots, ref elapsed, updateController: false);
        }
    }

    internal static void RunChunk(
        INesTestMachine machine,
        int chunkSize,
        NesControllerButton buttons,
        long maximumPpuDots,
        ref long elapsed,
        bool updateController = true)
    {
        if (updateController)
        {
            machine.SetControllerState(0, buttons);
        }
        var dots = (int)Math.Min(chunkSize, maximumPpuDots - elapsed);
        if (dots <= 0)
        {
            return;
        }
        machine.RunForPpuDots(dots);
        elapsed += dots;
    }
}
