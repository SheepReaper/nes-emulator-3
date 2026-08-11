using System.Reflection;

namespace Sheep.Emulation.Nes.Tests;

/// <summary>
/// Reflection helpers for inspecting and mutating internal PPU state during testing.
/// </summary>
internal static class PpuReflectionHelper
{
    internal static void SetPrivateField<T>(Ppu ppu, string name, T value)
    {
        var field = typeof(Ppu).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(ppu, value);
            return;
        }

        var unitsField = typeof(Ppu).GetField("_u", BindingFlags.Instance | BindingFlags.NonPublic);
        if (unitsField != null)
        {
            var units = unitsField.GetValue(ppu);
            if (units is PpuUnits ppuUnits)
            {
                var innerField = typeof(PpuTiming).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (innerField != null)
                {
                    innerField.SetValue(ppuUnits.Time, value);
                    return;
                }
            }
        }

        throw new InvalidOperationException($"Field {name} not found.");
    }

    internal static T GetPrivateField<T>(Ppu ppu, string name)
    {
        var field = typeof(Ppu).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            return (T)field.GetValue(ppu)!;
        }

        var unitsField = typeof(Ppu).GetField("_u", BindingFlags.Instance | BindingFlags.NonPublic);
        if (unitsField != null)
        {
            var units = unitsField.GetValue(ppu);
            if (units is PpuUnits ppuUnits)
            {
                var innerField = typeof(PpuTiming).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (innerField != null)
                {
                    return (T)innerField.GetValue(ppuUnits.Time)!;
                }
            }
        }

        throw new InvalidOperationException($"Field {name} not found.");
    }
}
