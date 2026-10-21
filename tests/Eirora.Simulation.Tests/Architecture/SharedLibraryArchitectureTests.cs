using Eirora.Contracts;
using Eirora.Simulation.Tests.Architecture.Fixtures;
using Eirora.WorldGeneration;
using Mono.Cecil;

namespace Eirora.Simulation.Tests.Architecture;

/// <summary>
/// The libraries shared with Unity stay engine-agnostic (ADR-0002), and the simulation and the map generators stay
/// pure (INV-D2, INV-D3, ADR-0010).
/// </summary>
public sealed class SharedLibraryArchitectureTests
{
    private static readonly string[] s_worldGenerationReferences = ["Eirora.Simulation", "netstandard"];

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

    // ADR-0010: the generators build on the simulation's map and nothing else.
    [Fact]
    public void WorldGenerationReferencesOnlyNetStandardAndTheSimulation()
    {
        using var assembly = AssemblyDefinition.ReadAssembly(typeof(WorldGenerators).Assembly.Location);

        var references = assembly.MainModule.AssemblyReferences.Select(reference => reference.Name).Order(StringComparer.Ordinal);

        Assert.Equal(s_worldGenerationReferences, references);
    }

    public static TheoryData<string> PureAssemblyPaths =>
    [
        typeof(RulesVersion).Assembly.Location,
        typeof(WorldGenerators).Assembly.Location,
    ];

    [Theory]
    [MemberData(nameof(PureAssemblyPaths))]
    public void SimulationAndGeneratorsNeverReadTheOutsideWorldOrUseFloatingPoint(string assemblyPath)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

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
    [InlineData(nameof(ImpureFixture.ReadsTickCount), "System.Environment")]
    [InlineData(nameof(ImpureFixture.ReadsTheRuntime), "System.Reflection.Assembly")]
    public void ScannerCatchesEachKindOfImpurity(string method, string expectedViolation)
    {
        var violations = ScanFixture();

        Assert.Contains(violations, v => v.Contains($"::{method}(", StringComparison.Ordinal) && v.Contains(expectedViolation, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(nameof(ImpureFixture.FormatsNumberByCulture), "culture-dependent System.Int32::ToString")]
    [InlineData(nameof(ImpureFixture.ParsesNumberByCulture), "culture-dependent System.Int32::Parse")]
    [InlineData(nameof(ImpureFixture.ComparesStringsByCulture), "culture-dependent System.String::Compare")]
    [InlineData(nameof(ImpureFixture.StartsWithByCulture), "culture-dependent System.String::StartsWith")]
    [InlineData(nameof(ImpureFixture.LowersByCulture), "culture-dependent System.String::ToLower")]
    [InlineData(nameof(ImpureFixture.SortsStringsByCulture), "culture-dependent System.Collections.Generic.SortedSet`1::.ctor without a comparer")]
    [InlineData(nameof(ImpureFixture.AppendsNumberByCulture), "culture-dependent System.Text.StringBuilder::Append of a number")]
    [InlineData(nameof(ImpureFixture.OrdersStringsByCulture), "culture-dependent System.Linq.Enumerable::OrderBy of strings without a comparer")]
    [InlineData(nameof(ImpureFixture.SortsStringListByCulture), "culture-dependent System.Collections.Generic.List`1::Sort of strings without a comparer")]
    [InlineData(nameof(ImpureFixture.SortsStringArrayByCulture), "culture-dependent System.Array::Sort of strings without a comparer")]
    [InlineData(nameof(ImpureFixture.IteratesDictionary), "hash order System.Collections.Generic.Dictionary`2::GetEnumerator")]
    [InlineData(nameof(ImpureFixture.IteratesDictionaryKeys), "hash order System.Collections.Generic.Dictionary`2::get_Keys")]
    [InlineData(nameof(ImpureFixture.IteratesReadOnlyDictionary), "hash order System.Collections.Generic.IEnumerable`1::GetEnumerator over key-value pairs")]
    [InlineData(nameof(ImpureFixture.IteratesReadOnlyDictionaryValues), "hash order System.Collections.Generic.IReadOnlyDictionary`2::get_Values")]
    [InlineData(nameof(ImpureFixture.QueriesDictionary), "hash order System.Linq.Enumerable::Count over key-value pairs")]
    [InlineData(nameof(ImpureFixture.CopiesDictionary), "hash order System.Collections.Generic.List`1::.ctor over key-value pairs")]
    public void ScannerCatchesWhatDiffersBetweenRuntimesAndDevices(string method, string expectedViolation)
    {
        var violations = ScanFixture();

        Assert.Contains(violations, v => v.Contains($"::{method}(", StringComparison.Ordinal) && v.Contains(expectedViolation, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(nameof(ImpureFixture.FormatsNumberInvariantly))]
    [InlineData(nameof(ImpureFixture.FindsCharacterOrdinally))]
    [InlineData(nameof(ImpureFixture.SortsStringsOrdinally))]
    [InlineData(nameof(ImpureFixture.AppendsNumberInvariantly))]
    [InlineData(nameof(ImpureFixture.OrdersStringsOrdinally))]
    [InlineData(nameof(ImpureFixture.IteratesSortedDictionary))]
    public void ScannerAllowsInvariantAndOrdinalForms(string method)
    {
        var violations = ScanFixture();

        Assert.DoesNotContain(violations, v => v.Contains($"::{method}(", StringComparison.Ordinal));
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

    [Fact]
    public void ScannerAllowsTheThreadCheckInCompilerGeneratedIterators()
    {
        var violations = ScanFixture();

        Assert.DoesNotContain(violations, v => v.Contains(nameof(ImpureFixture.IteratesWithYield), StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ScanFixture()
    {
        using var assembly = AssemblyDefinition.ReadAssembly(typeof(ImpureFixture).Assembly.Location);
        var fixture = assembly.MainModule.GetType(typeof(ImpureFixture).FullName);
        return PurityScanner.FindViolations([fixture]);
    }
}
