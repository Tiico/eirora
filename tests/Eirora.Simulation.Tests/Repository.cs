namespace Eirora.Simulation.Tests;

/// <summary>Files the tests keep in the repository: golden saves and maps, and the replay corpus.</summary>
internal static class Repository
{
    /// <summary>The repository's root folder, the one with Eirora.slnx.</summary>
    public static readonly string Root = FindRoot();

    /// <summary>A path under the repository's root.</summary>
    public static string PathTo(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Eirora.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The tests run outside the repository.");
    }
}
