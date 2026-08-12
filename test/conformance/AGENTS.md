# Conformance Harness Guidance

- Read the corresponding source under `test-roms/nes-test-roms` before interpreting a terse ROM failure label. Locate the byte or instruction distinguishing the boundary; do not infer direction from names such as “sooner” or “later.”
- Distinguish emulator failures from harness navigation failures. Interactive ROMs use controller strobing, OAM DMA synchronization, and menu loops before entering a micro-test.
- Decode status protocols from ROM source. AccuracyCoin uses `(ErrorCode << 2) | 0x02`; raw `$0A` and `$0E` are encoded outcomes, not literal test indexes.
- Successful `ppu_vbl_nmi/10-even_odd_timing` and `mmc3_test_2/4-scanline_timing` paths require substantially more than ten million PPU dots. Do not lower their ceilings based on an earlier failing path.
- Use bounded debugger traces around a real boundary. Keep full logs as artifacts and add the smallest focused regression test before implementation changes.
