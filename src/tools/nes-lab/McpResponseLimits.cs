using System.Text;

namespace Sheep.Nes.Lab;

public static class McpResponseLimits
{
    public const int MaximumDiscoveryBytes = 32 * 1024;
    public const int MaximumInspectionBytes = 64 * 1024;
    public const int MaximumContextBytes = 128 * 1024;
    public const int MaximumResourceBytes = 64 * 1024;
    public const int MaximumToolBytes = MaximumInspectionBytes;
    public const int EmergencyCeilingBytes = 256 * 1024;
    public const int MaximumPreviewBytes = 16 * 1024;

    public static void EnsureToolResponseIsBounded(string json)
        => EnsureResponseIsBounded(json, MaximumToolBytes);

    public static void EnsureResponseIsBounded(string json, int maximumBytes)
    {
        var byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > maximumBytes)
            throw new InvalidDataException(
                $"MCP response is {byteCount:N0} bytes; the limit is {maximumBytes:N0}. Use a bounded inspection operation.");
    }
}
