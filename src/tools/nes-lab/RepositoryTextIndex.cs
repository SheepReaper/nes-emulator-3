using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Sheep.Nes.Lab;

public sealed record RepositoryTextIndexBuild(string SourceFingerprint, int DocumentCount);
public sealed record RepositoryTextMatch(string Path, string Kind, string Excerpt, double Rank, string SourceHash);

public static class RepositoryTextIndex
{
    private static readonly string[] Extensions = [".cs", ".xaml", ".json", ".md", ".s", ".asm", ".inc"];

    public static RepositoryTextIndexBuild Build(string repositoryRoot, string databasePath)
    {
        var files = Enumerate(repositoryRoot).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var fingerprint = Fingerprint(repositoryRoot, files);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(databasePath)};Pooling=False");
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using (var schema = connection.CreateCommand())
        {
            schema.Transaction = transaction;
            schema.CommandText = """
DROP TABLE IF EXISTS repository_text;
DROP TABLE IF EXISTS repository_text_fts;
CREATE TABLE repository_text(id INTEGER PRIMARY KEY,path TEXT NOT NULL,kind TEXT NOT NULL,content TEXT NOT NULL,source_hash TEXT NOT NULL);
CREATE VIRTUAL TABLE repository_text_fts USING fts5(path,content,content='repository_text',content_rowid='id',tokenize='unicode61');
CREATE TABLE IF NOT EXISTS repository_text_metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL);
DELETE FROM repository_text_metadata;
""";
            schema.ExecuteNonQuery();
        }
        long id = 0;
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var searchable = content + "\n" + System.Text.RegularExpressions.Regex.Replace(content,
                "(?<=[a-z0-9])(?=[A-Z])", " ").ToLowerInvariant();
            using var insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO repository_text(id,path,kind,content,source_hash) VALUES($i,$p,$k,$c,$h); INSERT INTO repository_text_fts(rowid,path,content) VALUES($i,$p,$c);";
            insert.Parameters.AddWithValue("$i", ++id);
            insert.Parameters.AddWithValue("$p", Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'));
            insert.Parameters.AddWithValue("$k", Kind(file, repositoryRoot));
            insert.Parameters.AddWithValue("$c", searchable);
            insert.Parameters.AddWithValue("$h", Hash(Encoding.UTF8.GetBytes(content)));
            insert.ExecuteNonQuery();
        }
        using (var metadata = connection.CreateCommand())
        { metadata.Transaction = transaction; metadata.CommandText = "INSERT INTO repository_text_metadata(key,value) VALUES('sourceFingerprint',$v)"; metadata.Parameters.AddWithValue("$v", fingerprint); metadata.ExecuteNonQuery(); }
        transaction.Commit();
        return new(fingerprint, files.Length);
    }

    public static IReadOnlyList<RepositoryTextMatch> Search(string databasePath, string query, int maximumResults)
    {
        if (!File.Exists(databasePath) || string.IsNullOrWhiteSpace(query)) return [];
        var terms = query.Split([' ', '\t', '\r', '\n', '-', '_', '/', '$'], StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase).Take(12)
            .Select(term => $"\"{term.Replace("\"", "\"\"")}\"").ToArray();
        if (terms.Length == 0) return [];
        using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(databasePath)};Mode=ReadOnly;Pooling=False"); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
SELECT d.path,d.kind,snippet(repository_text_fts,1,'[',']',' … ',24),bm25(repository_text_fts),d.source_hash
FROM repository_text_fts JOIN repository_text d ON d.id=repository_text_fts.rowid
WHERE repository_text_fts MATCH $q ORDER BY bm25(repository_text_fts) LIMIT $m;
""";
        command.Parameters.AddWithValue("$q", string.Join(" OR ", terms)); command.Parameters.AddWithValue("$m", maximumResults);
        using var reader = command.ExecuteReader(); List<RepositoryTextMatch> result = [];
        while (reader.Read()) result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDouble(3), reader.GetString(4)));
        return result;
    }

    private static IEnumerable<string> Enumerate(string root) => new[] { "src", "test", "tasks" }.SelectMany(directory =>
    { var path = Path.Combine(root, directory); return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories) : []; })
        .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) && !path.Contains("\\bin\\") && !path.Contains("\\obj\\") &&
            !Path.GetFileName(path).StartsWith("gateway-corpus.", StringComparison.OrdinalIgnoreCase));
    private static string Kind(string path, string root) { var relative = Path.GetRelativePath(root, path).Replace('\\', '/'); return relative.StartsWith("test/") ? "test" : Path.GetFileName(path).Equals("AGENTS.md", StringComparison.OrdinalIgnoreCase) ? "guidance" : Path.GetExtension(path) is ".s" or ".asm" or ".inc" ? "assembly" : "source"; }
    private static string Fingerprint(string root, IEnumerable<string> files) { using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); foreach (var file in files) { hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file))); hash.AppendData(File.ReadAllBytes(file)); } return Convert.ToHexStringLower(hash.GetHashAndReset()); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
