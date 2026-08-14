using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Sheep.Nes.Lab;

public static class NesLabMcpServer
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly()
            .WithPromptsFromAssembly()
            .WithResourcesFromAssembly()
            .WithListResourcesHandler((_, _) =>
            {
                var store = new ImmutableArtifactStore(Path.Combine(
                    Directory.GetCurrentDirectory(), ".artifacts", "nes-lab"));
                return ValueTask.FromResult(new ListResourcesResult
                {
                    Resources = store.List().Select(item => new Resource
                    {
                        Name = $"nes-lab-{item.Kind}-{item.Digest[..12]}",
                        Title = $"NES Lab {item.Kind} artifact",
                        Uri = ImmutableArtifactStore.Uri(item.Kind, item.Digest),
                        MimeType = item.MimeType,
                        Size = item.ByteCount,
                        Description = item.Pinned ? "Pinned immutable artifact" : "Recent immutable artifact"
                    }).ToList()
                });
            });
        await builder.Build().RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
