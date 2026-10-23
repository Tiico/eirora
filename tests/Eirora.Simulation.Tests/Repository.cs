namespace Eirora.Simulation.Tests;

/// <summary>The repository the tests run in, for the golden files and the replay corpus checked in beside them.</summary>
internal static class Repository
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Eirora.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The tests run outside the repository.");
    }
}
