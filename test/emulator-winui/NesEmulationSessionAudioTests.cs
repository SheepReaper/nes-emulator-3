using Sheep.Emulation.Nes;
using Xunit;

namespace EmuSheep.Tests;

public sealed class NesEmulationSessionAudioTests
{
    [Fact]
    public async Task ActiveAudioBackend_StartsOnlyAfterFiftyMillisecondsAreBuffered()
    {
        NesAudioPlayer.SimulateAvailable = true;
        try
        {
            await using var session = new NesEmulationSession(NesEmulationTestHelper.CreateMapperZeroRom());
            await session.InitializeAudioAsync();

            session.Start();
            await NesAudioPlayer.Started.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(NesAudioPlayer.BufferedSamplesWhenStarted >= NesSystem.AudioSampleRate / 20);
        }
        finally
        {
            NesAudioPlayer.ResetSimulation();
        }
    }

    [Fact]
    public async Task ActiveAudioBackend_RefillsToTargetAfterEachQuantum()
    {
        NesAudioPlayer.SimulateAvailable = true;
        try
        {
            await using var session = new NesEmulationSession(NesEmulationTestHelper.CreateMapperZeroRom());
            await session.InitializeAudioAsync();
            session.Start();
            await NesAudioPlayer.Started.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            NesAudioPlayer.ConsumeSamples(NesSystem.AudioSampleRate / 100);

            var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (NesAudioPlayer.BufferedSamples < NesSystem.AudioSampleRate / 20 &&
                   DateTime.UtcNow < timeout)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            Assert.True(NesAudioPlayer.BufferedSamples >= NesSystem.AudioSampleRate / 20);
        }
        finally
        {
            NesAudioPlayer.ResetSimulation();
        }
    }
}
