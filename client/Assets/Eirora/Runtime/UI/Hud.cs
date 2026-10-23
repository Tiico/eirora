using System;
using System.Collections.Generic;
using System.Linq;
using Eirora.Client.App;
using Eirora.Client.Health;
using Eirora.Client.Look;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using UnityEngine;
using UnityEngine.UIElements;
using MapTerrain = Eirora.Simulation.Map.Terrain;

namespace Eirora.Client.UI
{
    /// <summary>
    /// The prototype's interface, built in code with UI Toolkit (NFR-14): forces and stock at the top, actions at
    /// the bottom, a sheet for the selected tile, self-care input, the daily report, the account and the night view
    /// (RST-03).
    /// </summary>
    public sealed partial class Hud
    {
        private readonly Func<GameCommand, CommandResult> _apply;
        private readonly IHealthControls? _health;
        private bool _explainingHealth;
        private bool _moreBuildingsShown;
        private readonly VisualElement _root;
        private readonly VisualElement _screen;
        private readonly VisualElement _top;
        private readonly VisualElement _middle;
        private readonly VisualElement _bottom;
        private readonly VisualElement _sheet;
        private readonly VisualElement _bar;
        private readonly Dictionary<Force, Label> _forceValues = new();
        private readonly Label _resources;
        private readonly Label _status;
        private readonly SelfCareForm _selfCare = new();
        private Label? _toast;
        private VisualElement? _setup;
        private World? _world;
        private IReadOnlyList<GameEvent> _sinceDawn = Array.Empty<GameEvent>();
        private Sheet _open;
        private Hex? _tile;
        private Hex? _fog;

        public Hud(VisualElement root, Func<GameCommand, CommandResult> apply, IHealthControls? health = null)
        {
            _apply = apply;
            _health = health;
            _root = root;
            _root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Eirora"));
            _root.pickingMode = PickingMode.Ignore;

            _screen = Container("screen", _root);
            _top = Container("card top", _screen);
            _middle = Container("middle", _screen);
            _middle.style.flexGrow = 1;
            _bottom = Container("bottom", _screen);
            _sheet = Container("card sheet", _bottom);
            _bar = Container("bar", _bottom);

            var forces = Container("forces", _top);
            foreach (var force in new[] { Force.Exploration, Force.BuildPower, Force.DawnPower })
            {
                var chip = Container("force", forces);
                chip.Add(new ForceIcon(force));
                _forceValues[force] = Text("force-value", chip, "0");
                Text("force-name", chip, Texts.Name(force));
            }

            _resources = Text("resources resource", _top, string.Empty);
            _status = Text("status", _top, string.Empty);
            _sheet.style.display = DisplayStyle.None;
            _top.style.display = DisplayStyle.None;
        }

        private enum Sheet
        {
            None,
            Tile,
            Fog,
            SelfCare,
            Report,
            Account,
        }

        /// <summary>The selected tile changed; the island marks it.</summary>
        public event Action<Hex?>? SelectionChanged;

        /// <summary>First launch: choose the rest period before the world begins (D-50).</summary>
        public void ShowSetup(Action<RestSchedule> begin)
        {
            int start = 23 * 60, duration = 8 * 60;
            var card = Container("card setup", _middle);
            _setup = card;
            Text("setup-title", card, "Välkommen till Eirora");
            Text("sheet-text", card, "När du vilar, vilar byn. Under viloperioden kan du titta på din värld, men inte ge order.");
            Label? startValue = null, durationValue = null;
            void Show()
            {
                startValue!.text = Clock(start);
                durationValue!.text = $"{duration / 60} h" + (duration % 60 == 0 ? string.Empty : " 30 min");
            }

            startValue = Stepper(card, "Viloperioden börjar", () => { start = (start + 1440 - 15) % 1440; Show(); }, () => { start = (start + 15) % 1440; Show(); });
            durationValue = Stepper(
                card,
                "Längd",
                () => { duration = Math.Max(RestSchedule.MinimumMinutes, duration - 30); Show(); },
                () => { duration = Math.Min(RestSchedule.MaximumMinutes, duration + 30); Show(); });
            Show();
            Button("button button-wide button-primary", card, "Börja", () =>
            {
                CloseSetup();
                begin(new RestSchedule(start, duration));
            });

            // An existing account on a new phone skips the tour: the world comes down from the cloud save (ACC-01).
            if (_account is { IsSignedIn: false } account && account.Providers.Count > 0)
            {
                Text("sheet-text", card, "Har du spelat förut? Logga in, så hämtas din ö från kontot.");
                foreach (var provider in account.Providers)
                {
                    Button("button button-wide", card, $"Logga in med {Texts.Provider(provider)}", () => Run(account.SignInAsync(provider), "Du är inloggad."));
                }
            }

            Text("build", card, Texts.Build(Application.version));
        }

        /// <summary>Takes the first-launch card away, as when a world arrived from the cloud save while it was shown.</summary>
        private void CloseSetup()
        {
            if (_setup is { } setup)
            {
                _setup = null;
                _middle.Remove(setup);
            }
        }

        /// <summary>
        /// The saved world cannot be read by this build. Nothing is overwritten; starting anew keeps the old save aside.
        /// </summary>
        public void ShowUnreadableSave(SaveProblem problem, System.Action startAnew)
        {
            var card = Container("card setup", _middle);
            Text("setup-title", card, "Din värld väntar");
            if (problem == SaveProblem.FromNewerVersion)
            {
                Text("sheet-text", card, "Din värld sparades av en nyare version av Eirora. Uppdatera appen för att spela vidare. Världen är kvar och ingenting har skrivits över.");
                Text("sheet-text", card, "Du kan också börja en ny värld. Den gamla sparas då undan.");
            }
            else if (problem == SaveProblem.FromOlderPrototype)
            {
                Text("sheet-text", card, "Din värld kommer från den första prototypen. Nu bygger byborna på brickorna i stället för på bestämda platser, och den gamla världen skulle bli en annan om den spelades upp igen. Den finns kvar på telefonen och ingenting har skrivits över.");
                Text("sheet-text", card, "Börja en ny värld. Den gamla sparas då undan.");
            }
            else
            {
                Text("sheet-text", card, "Den sparade världen och dess reservkopia kunde inte läsas. De finns kvar på telefonen och ingenting har skrivits över.");
                Text("sheet-text", card, "Du kan börja en ny värld. Den gamla sparas då undan, så att den kan undersökas.");
            }

            Button("button button-wide", card, "Börja en ny värld", () =>
            {
                _middle.Remove(card);
                startAnew();
            });
            Text("build", card, Texts.Build(Application.version));
        }

        public void Show(World world, IReadOnlyList<GameEvent> sinceDawn)
        {
            CloseSetup();
            _world = world;
            _sinceDawn = sinceDawn;
            _top.style.display = DisplayStyle.Flex;
            ApplySafeArea();

            foreach (var force in _forceValues.Keys)
            {
                _forceValues[force].text = world.Available(force).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            _resources.text = string.Join("   ", Enum.GetValues(typeof(Resource)).Cast<Resource>()
                .Select(r => $"{Capital(Texts.Name(r))} {world.Stock(r)}"));
            string upcoming = world.Phase == Phase.Resting
                ? $"Världen öppnar klockan {Clock(RestEnd(world))}"
                : $"Byn vilar klockan {Clock(world.RestSchedule.StartMinuteOfDay)}";
            _status.text = $"Spelardygn {world.GameDay} · {world.Villagers} av {world.Housing} bybor · {upcoming}";

            ShowNight(world);
            ShowBar(world);

            // A lifted tile is no longer under the fog: its sheet closes and the island shows what was there.
            if (_open == Sheet.Fog && _fog is Hex lifted && world.IsKnown(lifted))
            {
                Select(null);
                return;
            }

            ShowSheet();
        }

        /// <summary>
        /// Opens the sheet for a known tile, which tells what it is good for and what can be built there (D-79), or
        /// closes it; a tile that bears nothing, such as the open sea, closes it too.
        /// </summary>
        public void Select(Hex? tile)
        {
            if (tile is Hex hex && _world is not null && _world.UsesOf(hex) == TileUse.None && _world.BuildingAt(hex) is null
                && !_world.Projects.Any(p => p.Tile == hex))
            {
                tile = null;
            }

            _tile = tile;
            _moreBuildingsShown = false;
            _fog = null;
            _open = tile is null ? Sheet.None : Sheet.Tile;
            SelectionChanged?.Invoke(tile);
            ShowSheet();
        }

        /// <summary>Lists the buildings that any land takes on the selected tile below what it is special for.</summary>
        public void ShowMoreBuildings()
        {
            _moreBuildingsShown = true;
            ShowSheet();
        }

        /// <summary>Opens the sheet for a tile under the fog (TASK-032).</summary>
        public void SelectFog(Hex hex)
        {
            _tile = null;
            _fog = hex;
            _open = Sheet.Fog;
            SelectionChanged?.Invoke(hex);
            ShowSheet();
        }

        /// <summary>Whether a tile lies under the fog, so that tapping it opens the fog's sheet.</summary>
        public bool IsFogged(Hex hex) => _world is not null && !_world.IsKnown(hex);

        public void OpenSelfCare() => Open(Sheet.SelfCare);

        public void OpenReport() => Open(Sheet.Report);

        /// <summary>Opens the explanation shown before Health Connect's own dialog (HLT-03).</summary>
        public void OpenHealthExplanation()
        {
            Open(Sheet.SelfCare);
            _explainingHealth = true;
            ShowSheet();
        }

        /// <summary>Whether a screen position is over the interface rather than the island.</summary>
        public bool IsOverInterface(Vector2 screenPosition)
        {
            var panel = _root.panel;
            if (panel is null)
            {
                return false;
            }

            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(point);
            return picked is not null && picked != _root && picked != _screen && picked != _middle && picked != _bottom;
        }

        public void Toast(string message)
        {
            if (_toast is not null)
            {
                _root.Remove(_toast);
            }

            var toast = Text("toast", _root, message);
            _toast = toast;
            toast.schedule.Execute(() =>
            {
                if (_toast == toast)
                {
                    _root.Remove(toast);
                    _toast = null;
                }
            }).StartingIn(2600);
        }

        private void ShowNight(World world)
        {
            _middle.Clear();
            if (world.Phase != Phase.Resting)
            {
                return;
            }

            var night = Container("card night", _middle);
            Text("night-title", night, "Byn vilar.");
            Text("night-text", night, $"Världen öppnar klockan {Clock(RestEnd(world))}.");
            Text("night-text", night, world.ExpeditionHoursLeft > 0
                ? "Medan byn sover mognar säden, skogen växer och expeditionen ligger för ankar."
                : "Medan byn sover mognar säden och skogen växer.");
        }

        private void ShowBar(World world)
        {
            _bar.Clear();
            Button("button", _bar, "Egenvård", () => Open(Sheet.SelfCare));
            if (world.Phase == Phase.Active)
            {
                var survey = Survey.NextCost(world);
                if (survey is int cost)
                {
                    CostButton("Kartlägg", Force.Exploration, cost, () => Run(new Survey()));
                }
                else
                {
                    Button("button", _bar, "Kartlagt", () => { }).SetEnabled(false);
                }
                CostButton("Rusta", Force.DawnPower, Rush.Cost, () => Run(new Rush())).SetEnabled(world.Projects.Count > 0);
            }

            Button("button", _bar, "Rapport", () => Open(Sheet.Report));
            if (_account is not null)
            {
                // Always there, also during rest (RST-07), but never in the way of playing (ACC-04).
                var account = Button("button button-icon", _bar, string.Empty, OpenAccount);
                account.Add(new AccountIcon());
            }
        }

        private void Open(Sheet sheet)
        {
            _open = _open == sheet ? Sheet.None : sheet;
            _explainingHealth = false;
            _confirmingDeletion = false;
            _showingRecoveryCode = false;
            _sendingKey = false;
            _ticketToSend = null;
            if (sheet == Sheet.SelfCare && _world is not null)
            {
                _selfCare.Reset(_world.GameDay);
            }

            if (_open != Sheet.Tile && _open != Sheet.Fog)
            {
                _tile = null;
                _fog = null;
                SelectionChanged?.Invoke(null);
            }

            ShowSheet();
        }

        private void ShowSheet()
        {
            _sheet.Clear();
            if (_open != Sheet.Account || !_sendingKey)
            {
                StopKeySending();
            }

            if (_world is null || _open == Sheet.None)
            {
                _sheet.style.display = DisplayStyle.None;
                return;
            }

            _sheet.style.display = DisplayStyle.Flex;
            Button("close", _sheet, "×", () => Select(null));
            switch (_open)
            {
                case Sheet.Tile when _tile is Hex tile:
                    TileSheet(_world, tile);
                    break;
                case Sheet.Fog when _fog is Hex fog:
                    FogSheet(_world, fog);
                    break;
                case Sheet.SelfCare:
                    if (_explainingHealth)
                    {
                        HealthExplanation();
                    }
                    else
                    {
                        _selfCare.Build(_sheet, command => Run(command), Stepper, ShowSheet, HealthSection);
                    }

                    break;
                case Sheet.Report:
                    ReportSheet();
                    break;
                case Sheet.Account:
                    AccountSheet();
                    break;
            }
        }

        /// <summary>
        /// A tile under the fog: lift it with Exploration when it lies at the edge of the known, or say what finds it
        /// (TASK-032). What lies under the fog is never shown.
        /// </summary>
        private void FogSheet(World world, Hex hex)
        {
            Text("sheet-title", _sheet, "Dimma");
            var rejection = world.CheckLiftFog(hex);
            if (rejection != Rejection.None)
            {
                Text("sheet-text", _sheet, Texts.Reason(rejection));
                return;
            }

            Text("sheet-text", _sheet, "Utforskningen lyfter dimman och visar vad som finns här.");
            AddAction("Lyft dimman", $"{LiftFog.Cost} Utforskning", world.Phase == Phase.Active && world.Available(Force.Exploration) >= LiftFog.Cost, () => Run(new LiftFog(hex.Q, hex.R)));
        }

        /// <summary>A known tile: its building or the work on it, or what it is good for and what can stand there (D-79).</summary>
        private void TileSheet(World world, Hex tile)
        {
            var uses = world.UsesOf(tile);
            string ground = Texts.Ground(world.Map, tile, uses);
            var building = world.BuildingAt(tile);
            var project = world.Projects.FirstOrDefault(p => p.Tile == tile && p.Kind == ProjectKind.Build);
            if (building is not null)
            {
                Text("sheet-title", _sheet, Texts.Name(building.Kind) + (building.Level == 2 ? " · nivå 2" : string.Empty));
                Text("sheet-subtitle", _sheet, $"{ground} · {(building.Staffed ? "i arbete" : "väntar på bybor")}");
                BuildingActions(world, building);
                return;
            }

            if (project is not null)
            {
                Text("sheet-title", _sheet, $"{Texts.Name(project.Building)} byggs");
                Text("sheet-subtitle", _sheet, $"{ground} · {Math.Ceiling(project.WorkLeft / 1000.0)} h arbete kvar");
                Text("sheet-text", _sheet, "Lediga bybor bygger medan de är vakna.");
                return;
            }

            Text("sheet-title", _sheet, ground);
            Text("sheet-subtitle", _sheet, Texts.Uses(uses));
            if (world.Map.TerrainAt(tile) == MapTerrain.Forest)
            {
                Text("sheet-text", _sheet, Texts.Timber(world.TimberLeft(tile)));
            }

            // What only this kind of tile allows comes first; the buildings that any land takes wait behind one row, so
            // that the sheet leaves the island in view, and unfold below it.
            var buildable = world.Content.Buildings
                .Where(kind => world.CheckBuild(kind, tile) == Rejection.None)
                .ToList();
            var special = buildable.Where(kind => world.Content.For(kind).Requirement != TileUse.Land).ToList();
            var anyLand = buildable.Except(special).ToList();
            bool folded = special.Count > 0 && anyLand.Count > 0 && !_moreBuildingsShown;
            foreach (var kind in folded ? special : special.Concat(anyLand))
            {
                var spec = world.Content.For(kind);
                AddAction(Texts.Name(kind), Texts.Cost(spec), Affordable(world, spec), () => Run(new Build(kind, tile)), Texts.Effect(kind, spec));
            }

            if (folded)
            {
                AddAction("Fler byggnader", Capital(Texts.List(anyLand.Select(k => Texts.Name(k).ToLowerInvariant()))), true, ShowMoreBuildings);
            }

            if (buildable.Count == 0 && (uses & TileUse.Crest) != 0)
            {
                Text("sheet-text", _sheet, "Här ska Vårdkasen stå.");
            }
            else if (buildable.Count == 0 && uses != TileUse.None)
            {
                Text("sheet-text", _sheet, "Bygg en brygga på ön först, så når byggarna hit.");
            }
        }

        private void BuildingActions(World world, Building building)
        {
            var spec = world.Content.For(building.Kind);
            if (building.Kind == BuildingKind.Jetty)
            {
                JettyActions(world, building);
                return;
            }

            if (!spec.Produces)
            {
                if (Texts.Purpose(building.Kind, spec) is string purpose)
                {
                    Text("sheet-text", _sheet, purpose);
                }

                return;
            }

            if (building.Staffed && !building.WorkdayToday)
            {
                AddAction("Dagsverke", $"{Workday.Cost} Gryningskraft · 50 % mer i dag", world.Available(Force.DawnPower) >= Workday.Cost, () => Run(new Workday(building.Tile)));
            }
            else if (building.WorkdayToday)
            {
                Text("sheet-text", _sheet, "Dagsverke i dag: 50 % mer.");
            }

            if (building.Level == 1 && !world.Projects.Any(p => p.Tile == building.Tile && p.Kind == ProjectKind.Upgrade))
            {
                AddAction("Uppgradera till nivå 2", Texts.Cost(world.Content.Upgrade), Affordable(world, world.Content.Upgrade), () => Run(new Upgrade(building.Tile)));
            }

            int steps = world.DepotDistance(building.Tile);
            if (!building.HasRoad && steps > 1 && steps < int.MaxValue
                && !world.Projects.Any(p => p.Tile == building.Tile && p.Kind == ProjectKind.Road))
            {
                AddAction("Väg till förrådet", $"{2 * steps} sten och {4 * steps} h arbete · mer når fram", world.Stock(Resource.Stone) >= 2 * steps, () => Run(new BuildRoad(building.Tile)));
            }
        }

        /// <summary>
        /// The jetty: on the home island the expedition and every sea route with the goods waiting for the ships; on
        /// another island its own route and store (economy-v1.md, section 5).
        /// </summary>
        private void JettyActions(World world, Building building)
        {
            if (building.Island != world.HomeIsland)
            {
                string name = Texts.IslandName(world, building.Island);
                bool sailing = world.SeaRoutes.Any(r => r.From == building.Island);
                Text("sheet-text", _sheet, sailing
                    ? $"Skeppen tar hem varorna från {name}, {SeaRoute.UnitsPerGameDay} per dygn."
                    : "Bygg en brygga på Hemön, så seglar skeppen hit och tar varorna hem.");
                Text("sheet-text", _sheet, Waiting(world, building.Island) is string goods
                    ? (sailing ? "Väntar på skeppen: " : "I lagret: ") + goods + "."
                    : sailing ? "Inget väntar på skeppen just nu." : "Lagret är tomt.");
                return;
            }

            if (world.ExpeditionHoursLeft > 0)
            {
                Text("sheet-text", _sheet, $"Expeditionen är ute. Om {world.ExpeditionHoursLeft} timmar med vakna bybor är den tillbaka.");
            }
            else if (world.ExpeditionFinds is Hex finds && !world.IsKnown(finds))
            {
                int cost = SendExpedition.Cost(world);
                AddAction("Skicka expedition", $"{cost} Utforskning · 32 h med vakna bybor · hittar land bortom horisonten",
                    world.Available(Force.Exploration) >= cost, () => Run(new SendExpedition()));
            }
            else if (world.ExpeditionFinds is Hex found)
            {
                string island = world.Map.IslandAt(found)!;
                if (!world.SeaRoutes.Any(r => r.From == island))
                {
                    Text("sheet-text", _sheet, $"{Capital(Texts.IslandName(world, island))} är hittad. Bygg en brygga där, så öppnar sjövägen.");
                }
            }
            else
            {
                Text("sheet-text", _sheet, "Ingen ö inom räckhåll är värd en expedition.");
            }

            if (world.SeaRoutes.Count > 0)
            {
                Text("sheet-text", _sheet, world.SeaRoutes.Count == 1
                    ? $"Sjövägen till {Texts.IslandName(world, world.SeaRoutes[0].From)} är öppen. Skeppen tar hem {SeaRoute.UnitsPerGameDay} varor per dygn."
                    : $"{world.SeaRoutes.Count} sjövägar är öppna. Skeppen tar hem {SeaRoute.UnitsPerGameDay} varor per dygn från varje ö.");
            }

            foreach (var route in world.SeaRoutes)
            {
                string island = Capital(Texts.IslandName(world, route.From));
                Text("sheet-text", _sheet, Waiting(world, route.From) is string goods
                    ? $"{island}: {goods} väntar på skeppen."
                    : $"{island}: inget väntar på skeppen.");
            }
        }

        /// <summary>The goods in an island's store, such as "12 fisk och 5 sten", or null when it is empty.</summary>
        private static string? Waiting(World world, string island)
        {
            var goods = Enum.GetValues(typeof(Resource)).Cast<Resource>()
                .Where(r => world.Stock(island, r) > 0)
                .Select(r => $"{world.Stock(island, r)} {Texts.Name(r)}")
                .ToList();
            return goods.Count == 0 ? null : Texts.List(goods);
        }

        /// <summary>The health store's part of the self-care sheet: connect it, or see what is read and change it there.</summary>
        private void HealthSection()
        {
            var status = _health?.Status;
            if (_health is null || status is null || status.Availability == HealthAvailability.Unavailable)
            {
                return;
            }

            if (status.Availability == HealthAvailability.UpdateRequired)
            {
                AddAction("Installera Health Connect", "Behövs för att läsa steg, träning och sömn från telefonen", true, _health.OpenSettings);
                return;
            }

            if (status.Granted.Count == 0)
            {
                AddAction("Koppla Health Connect", "Steg, träningspass och sömn läses när du öppnar spelet", true, () =>
                {
                    _explainingHealth = true;
                    ShowSheet();
                });
                return;
            }

            var names = status.Granted.OrderBy(h => h).Select(h => h switch
            {
                Habit.Steps => "steg",
                Habit.Workouts => "träningspass",
                _ => "sömn",
            });
            Text("sheet-text", _sheet, $"Health Connect: läser {string.Join(", ", names)} när du öppnar spelet.");
            AddAction("Ändra i Health Connect", "Välj vad spelet får läsa, eller ta bort behörigheten", true, _health.OpenSettings);
        }

        /// <summary>
        /// The explanation before the system dialog (HLT-03, ux-journeys.md, step 9); both choices weigh the same.
        /// </summary>
        private void HealthExplanation()
        {
            Text("sheet-title", _sheet, "Var ska krafterna komma ifrån?");
            Text("sheet-text", _sheet, "Eirora kan läsa tre saker från Health Connect: hur många steg du tar, vilka träningspass du gör och hur länge du sover. Inget annat: ingen plats, ingen puls och ingen sömnkvalitet.");
            Text("sheet-text", _sheet, "Allt stannar på din telefon. Ingen annan ser något om du inte själv väljer att dela det. Du kan pausa, se och radera allt när du vill.");
            Text("sheet-text", _sheet, "Vill du hellre skriva in själv fungerar spelet precis lika bra.");
            var choices = Container("bar", _sheet);
            Button("button", choices, "Koppla Health Connect", () =>
            {
                _explainingHealth = false;
                _health?.Connect(granted =>
                {
                    Toast(granted > 0 ? "Health Connect är kopplat." : "Inget kopplat. Du skriver in själv.");
                    ShowSheet();
                });
            });
            Button("button", choices, "Jag skriver in själv", () =>
            {
                _explainingHealth = false;
                ShowSheet();
            });
        }

        private void ReportSheet()
        {
            Text("sheet-title", _sheet, "Sedan gryningen");
            var lines = Texts.Report(_sinceDawn).ToList();
            if (lines.Count == 0)
            {
                Text("report-line", _sheet, "Byn arbetar i lugn takt.");
            }

            foreach (var line in lines.TakeLast(9))
            {
                Text("report-line", _sheet, line);
            }

            Text("build", _sheet, Texts.Build(Application.version));
        }

        private void Run(GameCommand command)
        {
            var result = _apply(command);
            if (!result.Accepted)
            {
                Toast(Texts.Reason(result.Rejection));
            }
            else if (command is RecordSelfCare)
            {
                _open = Sheet.None;
                Toast(_world?.Phase == Phase.Resting ? "Sparat. Krafterna kommer i gryningen." : "Sparat.");
            }
        }

        private void AddAction(string title, string cost, bool available, System.Action onClick, string? effect = null)
        {
            var button = new Button(onClick);
            button.AddToClassList("action");
            if (!available)
            {
                button.AddToClassList("action-unavailable");
            }

            if (effect is null)
            {
                Text("action-title", button, title);
            }
            else
            {
                // What the building does sits beside its name, so the sheet is no taller for it.
                var head = Container("action-head", button);
                Text("action-title", head, title);
                Text("action-effect", head, effect);
            }

            Text("action-cost", button, cost);
            _sheet.Add(button);
        }

        private Label Stepper(VisualElement parent, string label, System.Action minus, System.Action plus)
        {
            var row = Container("row", parent);
            Text("row-label", row, label);
            var stepper = Container("stepper", row);
            Button("stepper-button", stepper, "−", minus);
            var value = Text("stepper-value", stepper, string.Empty);
            Button("stepper-button", stepper, "+", plus);
            return value;
        }

        private void ApplySafeArea()
        {
            var safe = Screen.safeArea;
            float scale = _root.resolvedStyle.width > 0 ? _root.resolvedStyle.width / Screen.width : 1f;
            _screen.style.paddingTop = 36 + ((Screen.height - safe.yMax) * scale);
            _screen.style.paddingBottom = 40 + (safe.yMin * scale);
        }

        /// <summary>A bar button with its cost shown as the force's own shape and an amount (UX-05).</summary>
        private Button CostButton(string title, Force force, int cost, System.Action onClick)
        {
            var button = Button("button", _bar, string.Empty, onClick);
            Text("button-title", button, title);
            var row = Container("button-cost", button);
            row.Add(new ForceIcon(force, 30f));
            Text("button-cost-value", row, cost.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return button;
        }

        private static bool Affordable(World world, BuildingSpec spec) =>
            spec.Cost.All(c => world.Stock(c.Key) >= c.Value) && world.Available(Force.BuildPower) >= spec.BuildPower;

        private static int RestEnd(World world) =>
            (world.RestSchedule.StartMinuteOfDay + world.RestSchedule.DurationMinutes) % 1440;

        private static string Clock(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

        private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

        private static VisualElement Container(string classes, VisualElement parent)
        {
            var element = new VisualElement();
            foreach (var name in classes.Split(' '))
            {
                element.AddToClassList(name);
            }

            if (classes is "screen" or "middle" or "bottom")
            {
                element.pickingMode = PickingMode.Ignore;
            }

            parent.Add(element);
            return element;
        }

        private static Label Text(string classes, VisualElement parent, string text)
        {
            var label = new Label(text);
            foreach (var name in classes.Split(' '))
            {
                label.AddToClassList(name);
            }

            parent.Add(label);
            return label;
        }

        private static Button Button(string classes, VisualElement parent, string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            foreach (var name in classes.Split(' '))
            {
                button.AddToClassList(name);
            }

            parent.Add(button);
            return button;
        }

        /// <summary>Self-care input with steppers, so no keyboard is needed (HLT-07, D-41, D-42, D-53).</summary>
        private sealed class SelfCareForm
        {
            private int _today;
            private int _day;
            private int _steps;
            private int _activeMinutes;
            private int _workouts;
            private int? _sleepMinutes;
            private bool _touchedSteps;
            private bool _touchedWorkouts;

            public void Reset(int today)
            {
                _today = today;
                _day = today;
                _steps = 0;
                _activeMinutes = 0;
                _workouts = 0;
                _sleepMinutes = null;
                _touchedSteps = false;
                _touchedWorkouts = false;
            }

            public void Build(VisualElement sheet, Action<GameCommand> run, Func<VisualElement, string, System.Action, System.Action, Label> stepper, System.Action refresh, System.Action health)
            {
                Text("sheet-title", sheet, "Egenvård");
                Text("sheet-subtitle", sheet, "Skriv in det du gjort. Det räknas precis som data från telefonen.");
                health();
                var tabs = Container("tabs", sheet);
                for (int back = 0; back <= 1 && _today - back >= 0; back++)
                {
                    int day = _today - back;
                    var tab = Hud.Button("tab", tabs, back == 0 ? "I dag" : "I går", () => { _day = day; refresh(); });
                    if (day == _day)
                    {
                        tab.AddToClassList("tab-selected");
                    }
                }

                Label steps = null!, minutes = null!, workouts = null!, sleep = null!;
                void Show()
                {
                    steps.text = _steps.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("sv-SE"));
                    minutes.text = $"{_activeMinutes} min";
                    workouts.text = _workouts.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    sleep.text = _sleepMinutes is int m ? $"{m / 60} h {m % 60:00}" : "–";
                }

                steps = stepper(sheet, "Steg", () => { _steps = Math.Max(0, _steps - 500); _touchedSteps = true; Show(); }, () => { _steps = Math.Min(60_000, _steps + 500); _touchedSteps = true; Show(); });
                minutes = stepper(sheet, "eller aktiva minuter", () => { _activeMinutes = Math.Max(0, _activeMinutes - 10); _touchedSteps = true; Show(); }, () => { _activeMinutes = Math.Min(600, _activeMinutes + 10); _touchedSteps = true; Show(); });
                workouts = stepper(sheet, "Träningspass, minst 10 min", () => { _workouts = Math.Max(0, _workouts - 1); _touchedWorkouts = true; Show(); }, () => { _workouts = Math.Min(6, _workouts + 1); _touchedWorkouts = true; Show(); });
                sleep = stepper(sheet, "Sömn", () => { _sleepMinutes = Math.Max(0, (_sleepMinutes ?? 450) - 15); Show(); }, () => { _sleepMinutes = Math.Min(960, (_sleepMinutes ?? 435) + 15); Show(); });
                Show();

                Hud.Button("button button-wide button-primary", sheet, "Spara", () =>
                {
                    if (_touchedSteps)
                    {
                        int total = Math.Max(_steps, ForceRules.StepsFromActiveMinutes(_activeMinutes));
                        run(new RecordSelfCare(_day, Habit.Steps, ContributionSource.SelfReported, total));
                    }

                    if (_touchedWorkouts)
                    {
                        run(new RecordSelfCare(_day, Habit.Workouts, ContributionSource.SelfReported, _workouts));
                    }

                    if (_sleepMinutes is int slept)
                    {
                        run(new RecordSelfCare(_day, Habit.Sleep, ContributionSource.SelfReported, slept));
                    }
                });
            }
        }

        /// <summary>
        /// The forces' own shapes, so colour never carries meaning alone (UX-05): a four-pointed compass star,
        /// a gabled house and a sun on the horizon.
        /// </summary>
        private sealed class ForceIcon : VisualElement
        {
            private readonly Force _force;

            public ForceIcon(Force force, float size = 58f)
            {
                _force = force;
                style.width = size;
                style.height = size;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float w = contentRect.width, h = contentRect.height, cx = w / 2f, cy = h / 2f;
                p.fillColor = _force switch
                {
                    Force.Exploration => Palette.Exploration,
                    Force.BuildPower => Palette.BuildPower,
                    _ => Palette.DawnPower,
                };
                p.BeginPath();
                switch (_force)
                {
                    case Force.Exploration:
                        float k = 0.17f * w;
                        p.MoveTo(new Vector2(cx, 0f));
                        p.LineTo(new Vector2(cx + k, cy - k));
                        p.LineTo(new Vector2(w, cy));
                        p.LineTo(new Vector2(cx + k, cy + k));
                        p.LineTo(new Vector2(cx, h));
                        p.LineTo(new Vector2(cx - k, cy + k));
                        p.LineTo(new Vector2(0f, cy));
                        p.LineTo(new Vector2(cx - k, cy - k));
                        break;
                    case Force.BuildPower:
                        p.MoveTo(new Vector2(cx, 0.06f * h));
                        p.LineTo(new Vector2(w * 0.94f, h * 0.46f));
                        p.LineTo(new Vector2(w * 0.82f, h * 0.46f));
                        p.LineTo(new Vector2(w * 0.82f, h * 0.94f));
                        p.LineTo(new Vector2(w * 0.18f, h * 0.94f));
                        p.LineTo(new Vector2(w * 0.18f, h * 0.46f));
                        p.LineTo(new Vector2(w * 0.06f, h * 0.46f));
                        break;
                    default:
                        p.MoveTo(new Vector2(w * 0.08f, h * 0.72f));
                        p.Arc(new Vector2(cx, h * 0.72f), w * 0.42f, Angle.Degrees(180f), Angle.Degrees(360f));
                        p.LineTo(new Vector2(w * 0.92f, h * 0.72f));
                        p.ClosePath();
                        p.Fill();
                        p.BeginPath();
                        p.MoveTo(new Vector2(0f, h * 0.84f));
                        p.LineTo(new Vector2(w, h * 0.84f));
                        p.LineTo(new Vector2(w, h * 0.92f));
                        p.LineTo(new Vector2(0f, h * 0.92f));
                        break;
                }

                p.ClosePath();
                p.Fill();
            }
        }
    }
}
