namespace TesterWorkbench.Core.Workspace;

public sealed class WorkspaceScanner
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dotnet-home",
        ".git",
        ".pytest_cache",
        ".superpowers",
        ".venv",
        "__pycache__"
    };

    private static readonly HashSet<string> IncludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html",
        ".json",
        ".yaml",
        ".yml"
    };

    public WorkspaceNode Scan(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("Workspace path is required.", nameof(workspacePath));
        }

        var root = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Workspace path does not exist: {root}");
        }

        return ScanDirectory(root, root, "workspace");
    }

    private static WorkspaceNode ScanDirectory(string root, string directory, string name)
    {
        var children = Directory.EnumerateFileSystemEntries(directory)
            .Where(ShouldIncludeEntry)
            .Select(path => CreateNode(root, path))
            .OrderBy(node => node.Kind == WorkspaceNodeKind.File ? 1 : 0)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WorkspaceNode(
            name,
            directory,
            RelativePath(root, directory),
            WorkspaceNodeKind.Folder,
            children);
    }

    private static WorkspaceNode CreateNode(string root, string path)
    {
        if (Directory.Exists(path))
        {
            return ScanDirectory(root, path, Path.GetFileName(path));
        }

        return new WorkspaceNode(
            Path.GetFileName(path),
            path,
            RelativePath(root, path),
            WorkspaceNodeKind.File,
            Array.Empty<WorkspaceNode>());
    }

    private static bool ShouldIncludeEntry(string path)
    {
        var name = Path.GetFileName(path);
        if (Directory.Exists(path))
        {
            return !name.StartsWith(".", StringComparison.Ordinal) && !IgnoredDirectories.Contains(name);
        }

        return IncludedExtensions.Contains(Path.GetExtension(path));
    }

    private static string RelativePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "."
            ? string.Empty
            : relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
