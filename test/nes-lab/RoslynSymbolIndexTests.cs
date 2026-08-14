using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Sheep.Nes.Lab.Tests;

public sealed class RoslynSymbolIndexTests
{
    [Fact]
    public async Task Index_ProvidesDeclarationsReferencesCallersAndAffectedTests()
    {
        using var workspace = new AdhocWorkspace();
        var coreId = ProjectId.CreateNewId();
        var testsId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(coreId, VersionStamp.Default, "Core", "Core", LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)))
            .AddMetadataReference(coreId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(coreId), "Cpu.cs", SourceText.From("""
                namespace Demo;
                public sealed class Cpu { public void Tick() { } }
                public sealed class Driver { public void Run(Cpu cpu) => cpu.Tick(); }
                """), filePath: "src/Cpu.cs")
            .AddProject(ProjectInfo.Create(testsId, VersionStamp.Default, "Core.Tests", "Core.Tests", LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)))
            .AddMetadataReference(testsId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddProjectReference(testsId, new ProjectReference(coreId))
            .AddDocument(DocumentId.CreateNewId(testsId), "CpuTests.cs", SourceText.From("""
                namespace Demo.Tests;
                public sealed class CpuTests { public void TickWorks() => new Demo.Cpu().Tick(); }
                """), filePath: "test/CpuTests.cs");

        await using var index = await RoslynSymbolIndex.CreateAsync(solution,
            TestContext.Current.CancellationToken);
        var declaration = Assert.Single(index.FindDeclarations("Tick"));
        var references = await index.FindReferencesAsync(declaration.Id,
            TestContext.Current.CancellationToken);
        var callers = await index.FindCallersAsync(declaration.Id,
            TestContext.Current.CancellationToken);
        var affectedTests = await index.FindAffectedTestsAsync(declaration.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal("Core", declaration.ProjectName);
        Assert.Equal(2, references.Count);
        Assert.Contains(callers, caller => caller.ContainingSymbol.Contains("Driver.Run", StringComparison.Ordinal));
        Assert.Contains(affectedTests, test => test.ProjectName == "Core.Tests" &&
            test.ContainingSymbol.Contains("TickWorks", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Index_DistinguishesOverloadsByStableId()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "Core", "Core", LanguageNames.CSharp)
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(projectId), "Overloads.cs", SourceText.From(
                "public class C { public void M() {} public void M(int x) {} }"), filePath: "Overloads.cs");

        await using var index = await RoslynSymbolIndex.CreateAsync(solution,
            TestContext.Current.CancellationToken);

        var declarations = index.FindDeclarations("M");
        Assert.Equal(2, declarations.Count);
        Assert.Equal(2, declarations.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public async Task QueryFiltersAndSourceExtractionReturnTheRequestedBody()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "Core", "Core", LanguageNames.CSharp)
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(projectId), "Cpu.cs", SourceText.From("""
                namespace Hardware;
                public class Cpu { public void Tick() { var cycles = 2; } }
                public enum Other { Tick }
                """), filePath: "src/Cpu.cs");
        await using var index = await RoslynSymbolIndex.CreateAsync(solution,
            TestContext.Current.CancellationToken);

        var declaration = Assert.Single(index.FindDeclarations(new RoslynSymbolQuery(
            "Tick", Kind: "Method", Project: "Core", Namespace: "Hardware", FilePath: "Cpu.cs")));
        var source = await index.GetDeclarationSourceAsync(
            declaration.Id, 4096, TestContext.Current.CancellationToken);

        Assert.Contains("var cycles = 2", source.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("enum Other", source.Content, StringComparison.Ordinal);
    }
}
