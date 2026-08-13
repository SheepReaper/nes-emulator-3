# WinUI Interop Guidance

- Populate and submit an `AudioFrame` in this order:
  1. Acquire the buffer reference and `LockBuffer` lease.
  2. If `LockBuffer` fails or returns null, discard the frame, log the failure using the project's standard diagnostic logger at Warning severity (including the frame timestamp and the exception or HRESULT returned by `LockBuffer`), and stop.
  3. Write all frame data.
  4. Dispose the lock lease and buffer reference.
  5. Call `AudioFrameInputNode.AddFrame`.
  Never call `AddFrame` if any prior step failed or the frame is partially written; calling `AddFrame` while the lock is still held throws `UnauthorizedAccessException` and produces silence. If frame data writing fails partway through, dispose the lock lease and buffer reference normally (step 4) before discarding the frame; do not call `AddFrame`.
- Do not extract `IMemoryBufferByteAccess`, pointer conversion utilities, or the `AllowUnsafeBlocks` MSBuild property into a separate assembly; they must remain in the same project that calls `AudioFrameInputNode.AddFrame`.
- `AudioFrameBuffer.WithSpan` must consume the span inside the buffer-reference and lock lifetime. Do not return raw spans from `AudioFrameBuffer.WithSpan` or any extension method that wraps a buffer reference; all span consumption must occur within the extension method body before the lock is released.
- Prefer static callbacks with explicit state in quantum paths to avoid closure allocation.
