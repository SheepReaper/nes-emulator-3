namespace Sheep.Nes.Lab.Tests;

public sealed class GitDiffHunkProviderTests
{
    [Fact]
    public void Parse_PreservesRenameAndLineRanges()
    {
        const string diff = """
diff --git a/old.cs b/new.cs
similarity index 90%
rename from old.cs
rename to new.cs
@@ -10,2 +12,3 @@ class C
-old
+new
+line
""";
        var hunk = Assert.Single(GitDiffHunkProvider.Parse(diff, GitChangeCategory.Staged, "main", "abc"));
        Assert.Equal(GitChangeCategory.Renamed, hunk.Category);
        Assert.Equal("old.cs", hunk.OldPath);
        Assert.Equal("new.cs", hunk.NewPath);
        Assert.Equal((10, 2, 12, 3), (hunk.OldStart, hunk.OldCount, hunk.NewStart, hunk.NewCount));
        Assert.Equal(64, hunk.ContentHash.Length);
    }

    [Fact]
    public void Parse_KeepsDeletedFileEvidence()
    {
        const string diff = """
diff --git a/gone.cs b/gone.cs
deleted file mode 100644
@@ -1 +0,0 @@
-gone
""";
        var hunk = Assert.Single(GitDiffHunkProvider.Parse(diff, GitChangeCategory.Unstaged));
        Assert.Equal(GitChangeCategory.Deleted, hunk.Category);
        Assert.Equal("gone.cs", hunk.OldPath);
        Assert.Null(hunk.NewPath);
        Assert.Contains("-gone", hunk.Content);
    }
}
