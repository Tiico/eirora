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

            var actions = root.Query<Button>(className: "action").ToList()
                .Select(a => $"{Label(a, "action-title")} | {Label(a, "action-effect")} | {Label(a, "action-cost")}");
            Assert.That(actions, Is.EqualTo(new[]
            {
                "Huggarkoja | Fäller 0,5 timmer i timmen från var och en av de två rikaste skogsbrickorna den når. | 6 timmer · 8 h arbete",
                "Långhus | Ger plats åt åtta bybor. | 10 timmer · 15 plank · 10 sten · 60 Byggkraft · 40 h arbete",
                "Stuga | Ger plats åt fyra bybor. | 15 timmer · 5 sten · 32 h arbete",
                "Förråd | Rymmer 150 av varje vara. Byggnader nära förrådet får fram mer. | 10 timmer · 5 sten · 16 h arbete",
                "Sågverk | Gör 1 plank av 2 timmer i timmen. | 15 timmer · 5 sten · 30 Byggkraft · 24 h arbete",
                "Väveri | Gör 1 vadmal av 2 ull varannan timme. | 10 plank · 5 sten · 60 Byggkraft · 24 h arbete",
                "Utkiksvarde | Kartläggning och expeditioner kostar en fjärdedel mindre Utforskning. | 10 sten · 40 Byggkraft · 12 h arbete",
            }));
        }

        [Test]
        public void EveryProducingBuildingTellsItsRateAsEconomyV1Says()
        {
            var effects = ContentCatalog.Current.Buildings
                .Where(kind => ContentCatalog.Current.For(kind).Produces)
                .Select(kind => $"{Texts.Name(kind)} | {Texts.Effect(kind, ContentCatalog.Current.For(kind))}");

            Assert.That(effects, Is.EqualTo(new[]
            {
                "Fiskebod | Ger 1,25 fisk i timmen.",
                "Åker | Ger 0,5 säd i timmen. Den växer dygnet runt och skördas när byborna är vakna.",
                "Huggarkoja | Fäller 0,5 timmer i timmen från var och en av de två rikaste skogsbrickorna den når.",
                "Fårgård | Ger 0,4 ull i timmen. Den växer dygnet runt och klipps när byborna är vakna.",
                "Stenbrott | Ger 0,75 sten i timmen.",
                "Sågverk | Gör 1 plank av 2 timmer i timmen.",
                "Väveri | Gör 1 vadmal av 2 ull varannan timme.",
            }));
        }

        private static string Label(VisualElement action, string className) => action.Q<Label>(className: className)?.text ?? "–";
    }
}
