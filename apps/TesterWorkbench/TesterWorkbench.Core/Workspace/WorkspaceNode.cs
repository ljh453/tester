namespace TesterWorkbench.Core.Workspace;

public enum WorkspaceNodeKind
{
    Folder,
    File
}

public sealed record WorkspaceNode(
    string Name,
    string FullPath,
    string RelativePath,
    WorkspaceNodeKind Kind,
    IReadOnlyList<WorkspaceNode> Children)
{
    public IEnumerable<WorkspaceNode> Flatten()
    {
        yield return this;

        foreach (var child in Children)
        {
            foreach (var descendant in child.Flatten())
            {
                yield return descendant;
            }
        }
    }
}
