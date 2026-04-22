using System.IO.Compression;
using System.Text;
using OpenStaff.Skills.Models;

namespace OpenStaff.Skills.Services;

public sealed class GitHubSkillArchiveClient : IGitHubSkillArchiveClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public GitHubSkillArchiveClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ExtractedSkillArchive> DownloadAsync(
        SkillCatalogEntry catalogEntry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogEntry);

        var tempRoot = Path.Combine(Path.GetTempPath(), "openstaff-skills", Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(tempRoot, "repo.zip");
        var extractPath = Path.Combine(tempRoot, "extract");

        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(extractPath);

        try
        {
            var client = _httpClientFactory.CreateClient(OpenStaffSkillsDefaults.GitHubHttpClientName);
            using var response = await client.GetAsync(
                $"repos/{catalogEntry.Owner}/{catalogEntry.Repo}/zipball",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var targetStream = File.Create(archivePath))
            await using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                await sourceStream.CopyToAsync(targetStream, cancellationToken);
            }

            ZipFile.ExtractToDirectory(archivePath, extractPath);
            var repositoryRoot = Directory.GetDirectories(extractPath).FirstOrDefault()
                ?? throw new InvalidOperationException($"Repository archive '{catalogEntry.Owner}/{catalogEntry.Repo}' is empty.");
            var skillDirectory = ResolveSkillDirectory(repositoryRoot, catalogEntry.SkillId);
            var metadata = ReadSkillDocument(Path.Combine(skillDirectory, "SKILL.md"));

            return new ExtractedSkillArchive(tempRoot, skillDirectory, metadata);
        }
        catch
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);

            throw;
        }
    }

    private static string ResolveSkillDirectory(string repositoryRoot, string skillId)
    {
        var candidates = Directory.GetFiles(repositoryRoot, "SKILL.md", SearchOption.AllDirectories)
            .Select(skillFile => new SkillDirectoryCandidate(
                DirectoryPath: Path.GetDirectoryName(skillFile)!,
                Metadata: ReadSkillDocument(skillFile)))
            .Where(candidate =>
                string.Equals(Path.GetFileName(candidate.DirectoryPath), skillId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Metadata.Name, skillId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Metadata.DisplayName, skillId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => string.Equals(candidate.Metadata.Name, skillId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(candidate => candidate.DirectoryPath.Count(ch => ch == Path.DirectorySeparatorChar))
            .ToList();

        if (candidates.Count == 1)
            return candidates[0].DirectoryPath;

        if (candidates.Count > 1)
            return candidates[0].DirectoryPath;

        var fallback = Directory.GetFiles(repositoryRoot, "SKILL.md", SearchOption.AllDirectories)
            .Select(skillFile => Path.GetDirectoryName(skillFile)!)
            .ToList();
        if (fallback.Count == 1)
            return fallback[0];

        throw new InvalidOperationException($"Skill '{skillId}' was not found in '{repositoryRoot}'.");
    }

    private static SkillDocumentMetadata ReadSkillDocument(string skillFilePath)
    {
        if (!File.Exists(skillFilePath))
            throw new InvalidOperationException($"Required skill manifest '{skillFilePath}' was not found.");

        using var stream = File.OpenRead(skillFilePath);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var name = default(string);
        var description = default(string);

        var firstLine = reader.ReadLine();
        if (!string.Equals(firstLine?.Trim(), "---", StringComparison.Ordinal))
        {
            return new SkillDocumentMetadata
            {
                Name = Path.GetFileName(Path.GetDirectoryName(skillFilePath)),
                DisplayName = Path.GetFileName(Path.GetDirectoryName(skillFilePath))
            };
        }

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is null)
                break;

            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
                break;

            var trimmed = line.Trim();
            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                name = ExtractScalarValue(trimmed);
                continue;
            }

            if (trimmed.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                description = ExtractScalarValue(trimmed);
        }

        var fallbackName = Path.GetFileName(Path.GetDirectoryName(skillFilePath));
        return new SkillDocumentMetadata
        {
            Name = string.IsNullOrWhiteSpace(name) ? fallbackName : name,
            DisplayName = string.IsNullOrWhiteSpace(name) ? fallbackName : name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description
        };
    }

    private static string ExtractScalarValue(string yamlLine)
    {
        var separatorIndex = yamlLine.IndexOf(':');
        if (separatorIndex < 0 || separatorIndex == yamlLine.Length - 1)
            return string.Empty;

        return yamlLine[(separatorIndex + 1)..].Trim().Trim('\'', '"');
    }

    private sealed record SkillDirectoryCandidate(string DirectoryPath, SkillDocumentMetadata Metadata);
}
