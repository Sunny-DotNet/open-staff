using OpenStaff.Skills.Models;

namespace OpenStaff.Skills.Services;

public interface IGitHubSkillArchiveClient
{
    Task<ExtractedSkillArchive> DownloadAsync(SkillCatalogEntry catalogEntry, CancellationToken cancellationToken = default);
}

public sealed class ExtractedSkillArchive : IDisposable
{
    public ExtractedSkillArchive(string temporaryRootPath, string skillDirectoryPath, SkillDocumentMetadata document)
    {
        TemporaryRootPath = temporaryRootPath;
        SkillDirectoryPath = skillDirectoryPath;
        Document = document;
    }

    public string TemporaryRootPath { get; }

    public string SkillDirectoryPath { get; }

    public SkillDocumentMetadata Document { get; }

    public void Dispose()
    {
        if (!Directory.Exists(TemporaryRootPath))
            return;

        Directory.Delete(TemporaryRootPath, recursive: true);
    }
}

public sealed class SkillDocumentMetadata
{
    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }
}
