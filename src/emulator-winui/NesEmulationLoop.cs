using System.Diagnostics;

using Sheep.Emulation.Nes;
using Sheep.Emulation.Nes.Debugging;

namespace EmuSheep;

internal static class NesEmulationLoop
{
    private const int MaximumBatchPpuDots = 4_096;
    private const int AudioHighWatermarkSamples = NesSystem.AudioSampleRate / 20;

    internal static async Task RunAsync(
        NesSystem nes,
        NesSessionRunContext context,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var pacer = new EmulationClockPacer(
            nes.Timing.MasterClockHz,
            nes.Timing.CpuDivisor,
            Stopwatch.Frequency);
        var frameRateCounter = new ActualFrameRateCounter(Stopwatch.Frequency);
        ulong executedCpuClocks = 0;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var audioPlayer = context.GetAudioPlayer();
                var dotsToRun = MaximumBatchPpuDots;

                if (audioPlayer != null)
                {
                    var bufferedSamples = nes.BufferedAudioSampleCount;
                    if (audioPlayer.IsStarted)
                    {
                        if (bufferedSamples >= AudioHighWatermarkSamples)
                        {
                            await context.WaitForAudioDemandAsync(cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                    }
                    else if (bufferedSamples >= AudioHighWatermarkSamples)
                    {
                        audioPlayer.Start();
                        continue;
                    }
                }
                else
                {
                    var budget = pacer.GetBudget(
                        stopwatch.ElapsedTicks,
                        executedCpuClocks,
                        context.GetSpeedMultiplier());
                    if (budget.CpuClocksToRun == 0)
                    {
                        await NesEmulationTimingHelper.WaitForBudgetAsync(budget.DelayStopwatchTicks, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var requestedDots = budget.CpuClocksToRun * (ulong)nes.Timing.CpuDivisor / (ulong)nes.Timing.PpuDivisor;
                    dotsToRun = (int)Math.Clamp(requestedDots, 1UL, MaximumBatchPpuDots);
                }

                var result = nes.RunForPpuDots(dotsToRun);
                executedCpuClocks += result.CpuClocks;
                if (result.StopReason != NesRunStopReason.Completed)
                {
                    throw new InvalidOperationException($"Emulation stopped unexpectedly ({result.StopReason}).");
                }

                if (result.Frames > 0)
                {
                    context.PublishFrame(nes.TryAcquireFrame());
                    cancellationToken.ThrowIfCancellationRequested();
                    context.OnFrameAvailable();
                    if (frameRateCounter.TryRecord(result.Frames, stopwatch.ElapsedTicks, out var fps))
                    {
                        context.OnFrameRateAvailable(fps);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            context.OnFaulted(exception);
        }
    }
}
