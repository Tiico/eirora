using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using MapTerrain = Eirora.Simulation.Map.Terrain;

namespace Eirora.Client.UI
{
    /// <summary>
    /// Demolition from a building's sheet (D-38, TASK-047): a confirmation that shows exactly what comes back to the store
    /// and what does not, before anything happens (D-108), and why a building cannot be demolished yet. While the village
    /// rests nothing is offered (INV-T3).
    /// </summary>
    public sealed partial class Hud
    {
        private Hex? _demolishing;

        /// <summary>Opens the confirmation to demolish the building on a tile, as the action on its sheet does.</summary>
        public void AskToDemolish(Hex tile)
        {
            Select(tile);
            _demolishing = tile;
            ShowSheet();
        }

        /// <summary>
        /// Demolition beside the close button, so that it makes no sheet taller: it opens the confirmation, or says why the
        /// building cannot be demolished yet. While the village rests there is none (INV-T3).
        /// </summary>
        private void DemolitionButton(World world, Building building)
        {
            if (world.Phase != Phase.Active || world.Demolition(building.Tile) is null)
            {
                return;
            }

            Button("sheet-demolish", _sheet, "Riv", () =>
            {
                _demolishing = building.Tile;
                ShowSheet();
            });
        }

        /// <summary>
        /// The confirmation: what comes back, what does not fit, what waits at the building and goes to the store, what has
        /// grown and goes with it, the force that never comes back, and what becomes of the villagers, the housing and the
        /// tile. Nothing disappears that it has not shown (D-108).
        /// </summary>
        private void DemolitionSheet(World world, Demolition demolition)
        {
            var building = demolition.Building;
            var spec = world.Content.For(building.Kind);
            string definite = Texts.Definite(building.Kind).ToLowerInvariant();
            if (demolition.Rejection != Rejection.None)
            {
                Refused(demolition, definite);
                return;
            }

            Text("sheet-title", _sheet, $"Riva {definite}?");
            Text("sheet-subtitle", _sheet, "Det går inte att ångra");

            Text("sheet-text", _sheet, Texts.Goods(demolition.Returned) is string returned
                ? $"Tillbaka till lagret: {returned}."
                : "Lagret har inte plats för något av materialet.");
            if (Texts.Goods(demolition.Lost) is string lost)
            {
                Text("sheet-text", _sheet, $"Ryms inte i lagret och kommer inte tillbaka: {lost}.");
            }

            if (Texts.Goods(demolition.Emptied) is string emptied)
            {
                Text("sheet-text", _sheet, $"Det som väntar här går till lagret: {emptied}.");
            }

            if (demolition.Unharvested > 0 && spec.Recipe is Recipe recipe)
            {
                string grown = recipe.Product == Resource.Wool ? "Ullen som vuxit och inte klippts" : "Säden som vuxit och inte skördats";
                Text("sheet-text", _sheet, $"{grown} försvinner: {demolition.Unharvested} {Texts.Name(recipe.Product, demolition.Unharvested)}.");
            }

            if (demolition.CancelsUpgrade)
            {
                Text("sheet-text", _sheet, "Uppgraderingen i byggkön följer med, och arbetet på den går förlorat.");
            }

            int upgrades = building.Level - 1 + (demolition.CancelsUpgrade ? 1 : 0);
            int power = spec.BuildPower + (upgrades * world.Content.Upgrade.BuildPower);
            if (power > 0)
            {
                Text("sheet-text", _sheet, $"{power} {Texts.Name(Force.BuildPower)} kommer inte tillbaka.");
            }

            int freed = Logistics.IsDepot(building.Kind) ? building.Carriers : building.Staffed ? spec.Staff : 0;
            if (freed > 0)
            {
                Text("sheet-text", _sheet, freed == 1 ? "En bybo blir ledig och bygger." : $"{Capital(Texts.Villagers(freed))} blir lediga och bygger.");
            }

            int housing = world.Housing - spec.Housing;
            if (spec.Housing > 0 && world.Villagers > housing)
            {
                Text("sheet-text", _sheet, $"Sedan finns bostäder för {housing} av {world.Villagers} bybor. Ingen flyttar, men byn växer inte förrän det finns plats.");
            }

            Text("sheet-text", _sheet, Freed(world.Map, building.Tile));

            var choices = Container("bar", _sheet);
            Button("button button-primary", choices, "Riv", () => ConfirmDemolition(demolition));
            Button("button", choices, "Avbryt", StopDemolishing);
        }

        /// <summary>Why the building cannot be demolished yet, and for a full store how much must go first (INV-R1).</summary>
        private void Refused(Demolition demolition, string definite)
        {
            Text("sheet-title", _sheet, $"{Capital(definite)} kan inte rivas" + (demolition.Rejection == Rejection.NoRoomInStore ? " än" : string.Empty));
            if (demolition.Rejection == Rejection.NoRoomInStore)
            {
                Text("sheet-subtitle", _sheet, "Lagret rymmer inte öns varor utan " + definite);
                Text("sheet-text", _sheet, $"Använd eller skeppa först: {Texts.Goods(demolition.TooMuch)}.");
                Text("sheet-text", _sheet, "Ingen vara får försvinna när något rivs.");
            }
            else
            {
                Text("sheet-text", _sheet, Texts.Reason(demolition.Rejection));
            }

            var choices = Container("bar", _sheet);
            Button("button", choices, "Tillbaka", StopDemolishing);
        }

        private void StopDemolishing()
        {
            _demolishing = null;
            ShowSheet();
        }

        private void ConfirmDemolition(Demolition demolition)
        {
            var building = demolition.Building;
            string? returned = Texts.Goods(demolition.Returned);
            var result = _apply(new Demolish(building.Tile));
            if (!result.Accepted)
            {
                Toast(Texts.Reason(result.Rejection));
                return;
            }

            Toast(returned is null ? $"{Texts.Definite(building.Kind)} revs." : $"{Texts.Definite(building.Kind)} revs. {Capital(returned)} kom tillbaka till lagret.");
            Select(building.Tile);
        }

        /// <summary>What the tile is good for again once the building is gone (D-91).</summary>
        private static string Freed(WorldMap map, Hex tile)
        {
            var uses = map.Features.UsesOf(tile);
            return map.TerrainAt(tile) == MapTerrain.Forest ? "Rutan blir ledig, och skogen på den går att fälla igen."
                : (uses & TileUse.Farmland) != 0 ? "Rutan blir ledig, och åkermarken går att odla igen."
                : (uses & TileUse.Pasture) != 0 ? "Rutan blir ledig, och betesmarken går att använda igen."
                : "Rutan blir ledig.";
        }
    }
}
