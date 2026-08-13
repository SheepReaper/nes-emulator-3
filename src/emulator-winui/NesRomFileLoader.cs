using Microsoft.UI.Xaml;

using Windows.Storage;
using Windows.Storage.Pickers;

namespace EmuSheep;

internal static class NesRomFileLoader
{
    internal const ulong MaximumRomFileSize = 1024 * 1024;

    internal static async Task<StorageFile?> PickRomFileAsync(Window window)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add(".nes");

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

        return await picker.PickSingleFileAsync();
    }

    internal static async Task<byte[]> ReadStorageFileBytesAsync(StorageFile file)
    {
        var properties = await file.GetBasicPropertiesAsync();
        if (properties.Size > MaximumRomFileSize)
        {
            throw new InvalidDataException("The selected ROM is larger than the supported 1 MB limit.");
        }

        await using var fileStream = await file.OpenStreamForReadAsync();
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    internal static async Task<byte[]> ReadRomFromPathAsync(string romPath)
    {
        var fullPath = Path.GetFullPath(romPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".nes", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The startup ROM must be an iNES .nes file.");
        }

        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The startup ROM file was not found.", fullPath);
        }

        return (ulong)file.Length > MaximumRomFileSize
            ? throw new InvalidDataException("The startup ROM is larger than the supported 1 MB limit.")
            : await File.ReadAllBytesAsync(fullPath);
    }
}
