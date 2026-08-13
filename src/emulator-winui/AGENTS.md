# WinUI Emulator Host Guidance

- Use `AudioFrameBuffer.WithSpan`; do not redeclare `IMemoryBufferByteAccess` or add unsafe audio-buffer access here.
- Dispose successfully submitted frames from `AudioFrameCompleted`; dispose the frame in the local catch/error branch only when generation or submission fails, not in a finally block shared with the success path.
- Keep emulation off the UI thread.
- Per dispatcher tick, present only the latest available frame; discard intermediate frames without disposing them.
- Marshal all bitmap mutation through the dispatcher.
- Treat audio-device initialization failure as nonfatal and retain timer-paced silent emulation.
