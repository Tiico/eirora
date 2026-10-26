using Eirora.Simulation.Economy;
using Eirora.Simulation.Saving;
using static Eirora.Simulation.Tests.TestClock;

namespace Eirora.Simulation.Tests.Economy;

/// <summary>
/// The grown village (TASK-052): what the generators and the screenshot tour start from has every tier, step and building
/// of economy v2, so that what they prove and show covers them.
/// </summary>
public sealed class GrownVillageTests
{
    private static readonly string s_tourSave = Repository.PathTo("client", "Assets", "Eirora", "Resources", "Tour", "GrownVillage.bytes");

    [Fact]
    public void TheGrownVillageHasEveryTierStepAndBuildingOfEconomyTwo() => AssertGrown(GrownVillage.Create(At("2026-10-14 14:00", 2), Night));

    // The screenshot tour's last views are of the grown village a morning after it was written, with its buildings staffed
    // and its villagers at work (client: ScreenshotTour). The save is created when it is missing, which fails the test so
    // that it is checked in; an older save migrates on load as any other.
    [Fact]
    public void TheScreenshotToursGrownVillageIsCheckedInAndHasEverything()
    {
        if (!File.Exists(s_tourSave))
        {
            var save = Save.Begin(GrownVillage.Create(At("2026-10-14 14:00", 2), Night));
            save.AdvanceTo(At("2026-10-16 10:00", 2));
            Directory.CreateDirectory(Path.GetDirectoryName(s_tourSave)!);
            File.WriteAllBytes(s_tourSave, save.ToBytes());
            Assert.Fail($"Created the screenshot tour's grown village in {s_tourSave}; check it in.");
        }

        var world = Save.Load(File.ReadAllBytes(s_tourSave)).World;
        AssertGrown(world);
        Assert.All(world.Buildings.Where(b => world.Content.For(b.Kind).Step > 0 && world.Content.For(b.Kind).Staff > 0), b => Assert.True(b.Staffed, $"The {b.Kind} is not staffed."));
    }

    private static void AssertGrown(World world)
    {
        Assert.Equal(ContentCatalog.Current.Steps.Count, world.Step);
        Assert.All(Enum.GetValues<PopulationTier>(), tier => Assert.Contains(world.Settlements, s => s.VillagersOf(tier) > 0));
        Assert.All(Enum.GetValues<BuildingKind>().Where(k => ContentCatalog.Current.For(k).Step > 0), kind => Assert.Contains(world.Buildings, b => b.Kind == kind));
        Assert.Single(world.SeaRoutes);
        Assert.Contains(world.Islands, i => world.Knarrs(i) > 0);
        Assert.Contains(world.Buildings, b => b.KnarrHoursLeft > 0);
    }
}
