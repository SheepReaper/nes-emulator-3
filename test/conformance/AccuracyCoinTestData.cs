using Xunit;

namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Test cases for FocusedCpuBusBehavior conformance suite.
/// </summary>
public static class AccuracyCoinTestData
{
    public static TheoryData<string, int, int, ushort> FocusedTestCases => new()
    {
        // Suite 0: CPU Behavior
        { "ROM is not writable", 0, 0, 0x0405 },
        { "RAM Mirroring", 0, 1, 0x0403 },
        { "PC Wraparound", 0, 2, 0x044D },
        { "The Decimal Flag", 0, 3, 0x0474 },
        { "The B Flag", 0, 4, 0x0475 },
        { "Dummy read cycles", 0, 5, 0x0406 },
        { "Dummy write cycles", 0, 6, 0x0407 },
        { "Open Bus", 0, 7, 0x0408 },
        { "All NOP instructions", 0, 8, 0x047D },

        // Suite 1: Addressing mode wraparound
        { "Absolute Indexed", 1, 0, 0x046E },
        { "Zero Page Indexed", 1, 1, 0x046F },
        { "Indirect", 1, 2, 0x0470 },
        { "Indirect, X", 1, 3, 0x0471 },
        { "Indirect, Y", 1, 4, 0x0472 },
        { "Relative", 1, 5, 0x0473 },

        // Suite 2: Unofficial Instructions: SLO
        { "$03   SLO indirect,X", 2, 0, 0x0409 },
        { "$07   SLO zeropage", 2, 1, 0x040A },
        { "$0F   SLO absolute", 2, 2, 0x040B },
        { "$13   SLO indirect,Y", 2, 3, 0x040C },
        { "$17   SLO zeropage,X", 2, 4, 0x040D },
        { "$1B   SLO absolute,Y", 2, 5, 0x040E },
        { "$1F   SLO absolute,X", 2, 6, 0x040F },

        // Suite 3: Unofficial Instructions: RLA
        { "$23   RLA indirect,X", 3, 0, 0x0419 },
        { "$27   RLA zeropage", 3, 1, 0x041A },
        { "$2F   RLA absolute", 3, 2, 0x041B },
        { "$33   RLA indirect,Y", 3, 3, 0x041C },
        { "$37   RLA zeropage,X", 3, 4, 0x041D },
        { "$3B   RLA absolute,Y", 3, 5, 0x041E },
        { "$3F   RLA absolute,X", 3, 6, 0x041F },

        // Suite 4: Unofficial Instructions: SRE
        { "$43   SRE indirect,X", 4, 0, 0x0420 },
        { "$47   SRE zeropage", 4, 1, 0x047F },
        { "$4F   SRE absolute", 4, 2, 0x0422 },
        { "$53   SRE indirect,Y", 4, 3, 0x0423 },
        { "$57   SRE zeropage,X", 4, 4, 0x0424 },
        { "$5B   SRE absolute,Y", 4, 5, 0x0425 },
        { "$5F   SRE absolute,X", 4, 6, 0x0426 },

        // Suite 5: Unofficial Instructions: RRA
        { "$63   RRA indirect,X", 5, 0, 0x0427 },
        { "$67   RRA zeropage", 5, 1, 0x0428 },
        { "$6F   RRA absolute", 5, 2, 0x0429 },
        { "$73   RRA indirect,Y", 5, 3, 0x042A },
        { "$77   RRA zeropage,X", 5, 4, 0x042B },
        { "$7B   RRA absolute,Y", 5, 5, 0x042C },
        { "$7F   RRA absolute,X", 5, 6, 0x042D },

        // Suite 6: Unofficial Instructions: *AX
        { "$83   SAX indirect,X", 6, 0, 0x042E },
        { "$87   SAX zeropage", 6, 1, 0x042F },
        { "$8F   SAX absolute", 6, 2, 0x0430 },
        { "$97   SAX zeropage,Y", 6, 3, 0x0431 },
        { "$A3   LAX indirect,X", 6, 4, 0x0432 },
        { "$A7   LAX zeropage", 6, 5, 0x0433 },
        { "$AF   LAX absolute", 6, 6, 0x0434 },
        { "$B3   LAX indirect,Y", 6, 7, 0x0435 },
        { "$B7   LAX zeropage,Y", 6, 8, 0x0436 },
        { "$BF   LAX absolute,Y", 6, 9, 0x0437 },

        // Suite 7: Unofficial Instructions: DCP
        { "$C3   DCP indirect,X", 7, 0, 0x0438 },
        { "$C7   DCP zeropage", 7, 1, 0x0439 },
        { "$CF   DCP absolute", 7, 2, 0x043A },
        { "$D3   DCP indirect,Y", 7, 3, 0x043B },
        { "$D7   DCP zeropage,X", 7, 4, 0x043C },
        { "$DB   DCP absolute,Y", 7, 5, 0x043D },
        { "$DF   DCP absolute,X", 7, 6, 0x043E },

        // Suite 8: Unofficial Instructions: ISC
        { "$E3   ISC indirect,X", 8, 0, 0x043F },
        { "$E7   ISC zeropage", 8, 1, 0x0440 },
        { "$EF   ISC absolute", 8, 2, 0x0441 },
        { "$F3   ISC indirect,Y", 8, 3, 0x0442 },
        { "$F7   ISC zeropage,X", 8, 4, 0x0443 },
        { "$FB   ISC absolute,Y", 8, 5, 0x0444 },
        { "$FF   ISC absolute,X", 8, 6, 0x0445 },

        // Suite 9: Unofficial Instructions: SH*
        { "SHA indirect,Y", 9, 0, 0x0446 },
        { "SHA absolute,Y", 9, 1, 0x0447 },
        { "SHS absolute,Y", 9, 2, 0x0448 },
        { "SHY absolute,X", 9, 3, 0x0449 },
        { "SHX absolute,Y", 9, 4, 0x044A },
        { "$BB   LAE absolute,Y", 9, 5, 0x044B },

        // Suite 10: Unofficial Immediates
        { "$0B   ANC Immediate", 10, 0, 0x0410 },
        { "$2B   ANC Immediate", 10, 1, 0x0411 },
        { "$4B   ASR Immediate", 10, 2, 0x0412 },
        { "$6B   ARR Immediate", 10, 3, 0x0413 },
        { "$8B   ANE Immediate", 10, 4, 0x0414 },
        { "$AB   LXA Immediate", 10, 5, 0x0415 },
        { "$CB   AXS Immediate", 10, 6, 0x0416 },
        { "$EB   SBC Immediate", 10, 7, 0x0417 },

        // Suite 11: CPU Interrupts
        { "Interrupt flag latency", 11, 0, 0x0461 },
        { "NMI Overlap BRK", 11, 1, 0x0462 },
        { "NMI Overlap IRQ", 11, 2, 0x0463 },

        // Suite 12: APU Registers and DMA tests
        { "DMA + Open Bus", 12, 0, 0x046C },
        { "DMA + $2002 Read", 12, 1, 0x0488 },
        { "DMA + $2007 Read", 12, 2, 0x044C },
        { "DMA + $2007 Write", 12, 3, 0x044F },
        { "DMA + $4015 Read", 12, 4, 0x045D },
        { "DMA + $4016 Read", 12, 5, 0x045E },
        { "DMC DMA Bus Conflicts", 12, 6, 0x046B },
        { "DMC DMA + OAM DMA", 12, 7, 0x0477 },
        { "Explicit DMA Abort", 12, 8, 0x0479 },
        { "Implicit DMA Abort", 12, 9, 0x0478 },

        // Suite 13: APU Tests
        { "Length Counter", 13, 0, 0x0465 },
        { "Length Table", 13, 1, 0x0466 },
        { "Frame Counter IRQ", 13, 2, 0x0467 },
        { "Frame Counter 4-step", 13, 3, 0x0468 },
        { "Frame Counter 5-step", 13, 4, 0x0469 },
        { "Delta Modulation Channel", 13, 5, 0x046A },
        { "APU Register Activation", 13, 6, 0x045C },
        { "Controller Strobing", 13, 7, 0x045F },
        { "Controller Clocking", 13, 8, 0x047A },

        // Suite 15: PPU Behavior
        { "CHR ROM is not writable", 15, 0, 0x0485 },
        { "PPU Register Mirroring", 15, 1, 0x0404 },
        { "PPU Register Open Bus", 15, 2, 0x044E },
        { "PPU Read Buffer", 15, 3, 0x0476 },
        { "Palette RAM Quirks", 15, 4, 0x047E },
        { "Rendering Flag Behavior", 15, 5, 0x0486 },
        { "$2007 read w/ rendering", 15, 6, 0x048A },
        { "Attributes As Tiles", 15, 7, 0x0481 },

        // Suite 16: PPU VBlank Timing
        { "NMI at VBlank end", 16, 5, 0x0455 },

        // Suite 17: Sprite Evaluation
        { "INC $4014", 17, 8, 0x0480 },

        // Suite 19: CPU Behavior 2
        { "Instruction Timing", 19, 0, 0x0460 },
        { "Implied Dummy Reads", 19, 1, 0x046D },
        { "Branch Dummy Reads", 19, 2, 0x048B },
        { "JSR Edge Cases", 19, 3, 0x047C },
        { "Internal Data Bus", 19, 4, 0x0490 }
    };
}
