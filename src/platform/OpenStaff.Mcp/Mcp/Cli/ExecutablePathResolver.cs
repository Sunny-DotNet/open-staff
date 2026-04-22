namespace OpenStaff.Mcp.Cli;

/// <summary>
/// zh-CN: 解析命令名到可直接启动的可执行路径；在 Windows 上会按工作目录、PATH 和 PATHEXT 查找批处理/可执行文件。
/// en: Resolves a command name into a directly launchable executable path; on Windows it probes the working directory, PATH, and PATHEXT.
/// </summary>
public static class ExecutablePathResolver
{
    public static string ResolveExecutablePath(
        string fileName,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        if (TryResolveExecutablePath(fileName, workingDirectory, environmentVariables, out var resolvedPath))
            return resolvedPath;

        return fileName;
    }

    public static bool TryResolveExecutablePath(
        string fileName,
        string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environmentVariables,
        out string resolvedPath)
    {
        resolvedPath = fileName;

        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(fileName))
            return false;

        if (Path.IsPathRooted(fileName)
            || fileName.Contains(Path.DirectorySeparatorChar)
            || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        var candidates = EnumerateCandidateFileNames(fileName, environmentVariables);
        foreach (var candidateRoot in EnumerateCandidateRoots(workingDirectory, environmentVariables))
        {
            foreach (var candidateFileName in candidates)
            {
                var candidatePath = Path.Combine(candidateRoot, candidateFileName);
                if (!File.Exists(candidatePath))
                    continue;

                resolvedPath = candidatePath;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> EnumerateCandidateFileNames(
        string fileName,
        IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        var fileNames = new List<string>();
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileNames.Add(fileName);
            return fileNames;
        }

        foreach (var extension in EnumeratePathExtensions(environmentVariables))
        {
            var candidate = fileName + extension;
            if (!fileNames.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                fileNames.Add(candidate);
        }

        if (!fileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            fileNames.Add(fileName);

        return fileNames;
    }

    private static IEnumerable<string> EnumerateCandidateRoots(
        string? workingDirectory,
        IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            yield return workingDirectory;

        var pathValue = environmentVariables?.TryGetValue("PATH", out var overriddenPath) == true
            ? overriddenPath
            : Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
            yield break;

        foreach (var segment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = segment.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(normalized))
                yield return normalized;
        }
    }

    private static IEnumerable<string> EnumeratePathExtensions(IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        var pathExtValue = environmentVariables?.TryGetValue("PATHEXT", out var overriddenPathExt) == true
            ? overriddenPathExt
            : Environment.GetEnvironmentVariable("PATHEXT");

        if (string.IsNullOrWhiteSpace(pathExtValue))
            pathExtValue = ".COM;.EXE;.BAT;.CMD";

        foreach (var extension in pathExtValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return extension.Length > 0 && extension[0] == '.'
                ? extension
                : "." + extension;
        }
    }
}
