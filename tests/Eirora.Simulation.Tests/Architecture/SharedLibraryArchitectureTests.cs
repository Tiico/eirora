using Eirora.Contracts;
using Eirora.Simulation.Tests.Architecture.Fixtures;
using Mono.Cecil;

namespace Eirora.Simulation.Tests.Architecture;

/// <summary>
/// The libraries shared with Unity stay engine-agnostic (ADR-0002) and the simulation stays pure (INV-D2, INV-D3).
/// </summary>
public sealed class SharedLibraryArchitectureTests
{
    public static TheoryData<string> SharedAssemblyPaths =>
    [
        typeof(RulesVersion).Assembly.Location,
        typeof(ServerInfo).Assembly.Location,
    ];

    [Theory]
    [MemberData(nameof(SharedAssemblyPaths))]
    public void SharedLibrariesReferenceOnlyNetStandard(string assemblyPath)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

        var references = assembly.MainModule.AssemblyReferences.Select(reference => reference.Name);

        Assert.All(references, name => Assert.Equal("netstandard", name));
    }

    [Fact]
    public void SimulationNeverReadsTheOutsideWorldOrUsesFloatingPoint()
    {
        using var assembly = AssemblyDefinition.ReadAssembly(typeof(RulesVersion).Assembly.Location);

        var violations = PurityScanner.FindViolations(assembly.MainModule.Types);

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData(nameof(ImpureFixture.ReadsWallClock), "System.DateTime::get_UtcNow")]
    [InlineData(nameof(ImpureFixture.ReadsSystemRandomness), "System.Random")]
    [InlineData(nameof(ImpureFixture.MakesGuid), "System.Guid::NewGuid")]
    [InlineData(nameof(ImpureFixture.ReadsFiles), "System.IO.File")]
    [InlineData(nameof(ImpureFixture.ReadsLocalTimeZone), "System.TimeZoneInfo::get_Local")]
    [InlineData(nameof(ImpureFixture.HashesString), "System.String::GetHashCode")]
    [InlineData(nameof(ImpureFixture.ComputesWithFloats), "ldc.r8")]
    [InlineData(nameof(ImpureFixture.CallsMath), "System.Math")]
    public void ScannerCatchesEachKindOfImpurity(string method, string expectedViolation)
    {
        var violations = ScanFixture();

        Assert.Contains(violations, v => v.Contains($"::{method}(", StringComparison.Ordinal) && v.Contains(expectedViolation, StringComparison.Ordinal));
    }

    [Fact]
    public void ScannerCatchesFloatingPointFields()
    {
        var violations = ScanFixture();

        Assert.Contains(violations, v => v.Contains("s_storedFloat: uses System.Double", StringComparison.Ordinal));
    }

    [Fact]
    public void ScannerDoesNotConfuseSystemIObservableWithSystemIo()
    {
        var violations = ScanFixture();

        Assert.DoesNotContain(violations, v => v.Contains(nameof(ImpureFixture.UsesInterfaceNamedLikeSystemIo), StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ScanFixture()
    {
        using var assembly = AssemblyDefinition.ReadAssembly(typeof(ImpureFixture).Assembly.Location);
        var fixture = assembly.MainModule.GetType(typeof(ImpureFixture).FullName);
        return PurityScanner.FindViolations([fixture]);
    }
}
