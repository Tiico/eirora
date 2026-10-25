using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// The content validator that CI runs over the catalog (INV-R4, economy-v2.md, section 3): every need can be made without
/// forces in at least two ways that start on different land, and no need takes its own tier to make. It grows with the
/// needs and tiers of steps 2 and 3.
/// </summary>
public sealed class ContentValidationTests
{
    [Fact]
    public void TheCatalogMeetsInvRFour()
    {
        Assert.Empty(ContentValidation.Problems(ContentCatalog.Current));
    }

    // Acceptance: given a catalog where a need has only one source without forces, when CI runs, the content is refused.
    [Fact]
    public void ANeedWithOnlyOneSourceWithoutForcesIsRefused()
    {
        var tannery = ContentCatalog.Current.For(BuildingKind.Tannery);
        var catalog = ContentCatalog.Current.Replacing(BuildingKind.Tannery, Like(tannery, buildPower: 30));

        Assert.Equal(["Clothes: fewer than two ways to make it without forces that start on different land."], ContentValidation.Problems(catalog));
    }

    // Two sources on the same land are one way to start, which a map without that land cannot make.
    [Fact]
    public void TwoSourcesOnTheSameLandAreRefused()
    {
        var needs = new[]
        {
            new NeedSpec(Need.Food, Resource.Fish),
            new NeedSpec(Need.Clothes, Resource.Wadmal, Resource.Hide),
        };

        Assert.Equal(["Food: fewer than two ways to make it without forces that start on different land."], ContentValidation.Problems(ContentCatalog.Current.WithNeeds(needs)));
    }

    // Wadmal counts without the weavery's Build Power only because the weavery can be built by hand (D-65), and only while
    // its chain's planks fit beside the quarry's in the start's 20.
    [Fact]
    public void AChainCountsWithoutForcesOnlyByHandAndWithinTheStartsPlanks()
    {
        var weavery = ContentCatalog.Current.For(BuildingKind.Weavery);

        var byHand = ContentCatalog.Current.Replacing(BuildingKind.Weavery, Like(weavery, handBuildDays: 0));
        var dear = ContentCatalog.Current.Replacing(BuildingKind.Weavery, Like(weavery, planks: 12));

        Assert.NotEmpty(ContentValidation.Problems(byHand));
        Assert.NotEmpty(ContentValidation.Problems(dear));
    }

    // Clothes are made by crofters, so the farmers who need them never wait for themselves.
    [Fact]
    public void ANeedThatTakesItsOwnTierToMakeIsRefused()
    {
        var tannery = ContentCatalog.Current.For(BuildingKind.Tannery);
        var catalog = ContentCatalog.Current.Replacing(BuildingKind.Tannery, Like(tannery, tier: PopulationTier.Farmers));

        Assert.Contains("Clothes: Hide takes Farmers to make, a tier that needs it.", ContentValidation.Problems(catalog));
    }

    private static BuildingSpec Like(BuildingSpec spec, int? buildPower = null, int? handBuildDays = null, int? planks = null, PopulationTier? tier = null)
    {
        var cost = new Dictionary<Resource, int>(spec.Cost);
        if (planks is int units)
        {
            cost[Resource.Planks] = units;
        }

        return new BuildingSpec(
            cost, spec.WorkHours, buildPower ?? spec.BuildPower, spec.Staff, spec.Requirement, spec.Housing, spec.MaxLevel, spec.Recipe,
            spec.Step, tier ?? spec.Tier, spec.Houses, handBuildDays ?? spec.HandBuildDays);
    }
}
