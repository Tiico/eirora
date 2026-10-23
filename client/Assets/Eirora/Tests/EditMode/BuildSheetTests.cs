using System.Linq;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Eirora.Client.Tests
{
    /// <summary>
    /// The build sheet tells what each building costs and does as economy v1 says (economy-v1.md, section 3), read from
    /// the content catalog rather than from texts of its own (TASK-042).
    /// </summary>
    public sealed class BuildSheetTests
    {
        [Test]
        public void ASiteBesideTheVillageOffersItsBuildingsWithTheirCostAndEffect()
        {
            var clock = new FixedClock(ScriptedWeek.At("2026-10-14 14:00"));
            var game = new GameSession(clock, null, () => PrototypeHome.Map);
            game.Begin(new RestSchedule(23 * 60, 8 * 60));
            var root = new VisualElement();
            var hud = new Hud(root, command => game.Apply(command));
            hud.Show(game.World!, game.SinceDawn);

            hud.Select(new Hex(-1, 0));
            hud.ShowMoreBuildings();

            var actions = root.Query<Button>(className: "action").ToList()
                .Select(a => $"{Label(a, "action-title")} | {Label(a, "action-effect")} | {Label(a, "action-cost")}");
            Assert.That(actions, Is.EqualTo(new[]
            {
                "Huggarkoja | 0,5 timmer/h per skogsbricka, högst två | 6 timmer · 8 h arbete",
                "Långhus | +8 bostadsplatser | 10 timmer · 15 plank · 10 sten · 60 Byggkraft · 40 h arbete",
                "Stuga | +4 bostadsplatser | 15 timmer · 5 sten · 32 h arbete",
                "Förråd | Lager 150 per vara | 10 timmer · 5 sten · 16 h arbete",
                "Sågverk | 2 timmer → 1 plank/h | 15 timmer · 5 sten · 30 Byggkraft · 24 h arbete",
                "Väveri | 2 ull → 1 vadmal per 2 h | 10 plank · 5 sten · 60 Byggkraft · 24 h arbete",
                "Utkiksvarde | 25 % mindre Utforskning | 10 sten · 40 Byggkraft · 12 h arbete",
            }));
        }

        [Test]
        public void EveryProducingBuildingTellsItsRateAsTheTableInEconomyV1Does()
        {
            var effects = ContentCatalog.Current.Buildings
                .Where(kind => ContentCatalog.Current.For(kind).Produces)
                .Select(kind => $"{Texts.Name(kind)} | {Texts.Effect(kind, ContentCatalog.Current.For(kind))}");

            Assert.That(effects, Is.EqualTo(new[]
            {
                "Fiskebod | 1,25 fisk/h",
                "Åker | 0,5 säd/h dygnet runt",
                "Huggarkoja | 0,5 timmer/h per skogsbricka, högst två",
                "Fårgård | 0,4 ull/h dygnet runt",
                "Stenbrott | 0,75 sten/h",
                "Sågverk | 2 timmer → 1 plank/h",
                "Väveri | 2 ull → 1 vadmal per 2 h",
            }));
        }

        [Test]
        public void AHouseTellsOnItsSheetHowManyItHousesAsTheCatalogSays()
        {
            var catalog = ContentCatalog.Current;

            Assert.That(Texts.Purpose(BuildingKind.Longhouse, catalog.For(BuildingKind.Longhouse)), Is.EqualTo("Ger plats åt åtta bybor."));
            Assert.That(Texts.Purpose(BuildingKind.Cottage, catalog.For(BuildingKind.Cottage)), Is.EqualTo("Ger plats åt fyra bybor."));
        }

        private static string Label(VisualElement action, string className) => action.Q<Label>(className: className)?.text ?? "–";
    }
}
