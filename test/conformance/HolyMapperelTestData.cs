using Xunit;

namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Test data definitions for Holy Mapperel ROM suites.
/// </summary>
public static class HolyMapperelTestData
{
    public static TheoryData<string, string> Cases => new()
    {
        { "M2_P128K_CR8K_V.nes", "c7e83755bd9adbb7c705ea9f29535442af7390444632f9f824fcf4c00632069b" },
        { "M3_P32K_C32K_H.nes", "499891c6d8c7a1e7631bdc601d9d624938842735f92fe1fda7b19ef9fae514b7" },
        { "M7_P128K_CR8K.nes", "4aa0050f36ae17e17701506821e5147df5b843bcdf2827468fa9c66e4b7ac1ba" },
        { "M11_P64K_C64K_V.nes", "ce96c35263788dec062aed329d951b351415a48bd1681cd7c6eb7328a342c321" },
        { "M28_P512K_CR32K.nes", "d06782c2a893049aad4832e914f0d921e3cd2af8ccf644f51ff76590c6f1ee49" },
        { "M34_P128K_CR8K_H.nes", "cb00e7b0092000b272f1c5bc341038da45031d44993d1a1abde864b5eafb1d85" }
    };
}
