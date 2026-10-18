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
using Eirora.Simulation.SelfCare;
using Eirora.Simulation.Time;
using UnityEngine;
using UnityEngine.UIElements;

namespace Eirora.Client.UI
{
    /// <summary>
    /// The prototype's interface, built in code with UI Toolkit (NFR-14): forces and stock at the top, actions at
    /// the bottom, a sheet for the selected place, self-care input, the daily report and the night view (RST-03).
    /// </summary>
    public sealed class Hud
    {
        private readonly Func<GameCommand, CommandResult> _apply;
        private readonly IHealthControls? _health;
        private bool _explainingHealth;
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
        private World? _world;
        private IReadOnlyList<GameEvent> _sinceDawn = Array.Empty<GameEvent>();
        private Sheet _open;
        private Site? _site;

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
            Site,
            SelfCare,
            Report,
        }

        /// <summary>The selected place changed; the island marks it.</summary>
        public event Action<Site?>? SelectionChanged;

        /// <summary>First launch: choose the rest period before the world begins (D-50).</summary>
        public void ShowSetup(Action<RestSchedule> begin)
        {
            int start = 23 * 60, duration = 8 * 60;
            var card = Container("card setup", _middle);
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
                _middle.Remove(card);
                begin(new RestSchedule(start, duration));
            });
            Text("build", card, Texts.Build(Application.version));
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
            ShowSheet();
        }

        /// <summary>Opens the sheet for a place on the island, or closes it.</summary>
        public void Select(Site? site)
        {
            _site = site;
            _open = site is null ? Sheet.None : Sheet.Site;
            SelectionChanged?.Invoke(site);
            ShowSheet();
        }

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
        }

        private void Open(Sheet sheet)
        {
            _open = _open == sheet ? Sheet.None : sheet;
            _explainingHealth = false;
            if (sheet == Sheet.SelfCare && _world is not null)
            {
                _selfCare.Reset(_world.GameDay);
            }

            if (_open != Sheet.Site)
            {
                _site = null;
                SelectionChanged?.Invoke(null);
            }

            ShowSheet();
        }

        private void ShowSheet()
        {
            _sheet.Clear();
            if (_world is null || _open == Sheet.None)
            {
                _sheet.style.display = DisplayStyle.None;
                return;
            }

            _sheet.style.display = DisplayStyle.Flex;
            Button("close", _sheet, "×", () => Select(null));
            switch (_open)
            {
                case Sheet.Site when _site is not null:
                    SiteSheet(_world, _site);
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
            }
        }

        private void SiteSheet(World world, Site site)
        {
            if (!world.IsRevealed(site.Id))
            {
                Text("sheet-title", _sheet, "Under dimman");
                Text("sheet-text", _sheet, "Något ligger gömt här. Kartlägg ön för att se vad.");
                return;
            }

            var building = world.Buildings.FirstOrDefault(b => b.Site == site);
            var project = world.Projects.FirstOrDefault(p => p.Site == site && p.Kind == ProjectKind.Build);
            if (building is not null)
            {
                Text("sheet-title", _sheet, Texts.Name(building.Kind) + (building.Level == 2 ? " · nivå 2" : string.Empty));
                Text("sheet-subtitle", _sheet, $"{Texts.Describe(site)} · {(building.Staffed ? "i arbete" : "väntar på bybor")}");
                BuildingActions(world, building);
                return;
            }

            if (project is not null)
            {
                Text("sheet-title", _sheet, $"{Texts.Name(project.Building)} byggs");
                Text("sheet-subtitle", _sheet, $"{Texts.Describe(site)} · {Math.Ceiling(project.WorkLeft / 1000.0)} h arbete kvar");
                Text("sheet-text", _sheet, "Lediga bybor bygger medan de är vakna.");
                return;
            }

            Text("sheet-title", _sheet, Texts.Describe(site));
            Text("sheet-subtitle", _sheet, "Vad ska stå här?");
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                var spec = BuildingSpec.For(kind);
                if ((site.Kinds & spec.Site) != 0)
                {
                    AddAction(Texts.Name(kind), Texts.Cost(spec), Affordable(world, spec), () => Run(new Build(kind, site.Id)));
                }
            }
        }

        private void BuildingActions(World world, Building building)
        {
            bool produces = building.Kind is not (BuildingKind.Longhouse or BuildingKind.Cottage or BuildingKind.Storehouse
                or BuildingKind.Jetty or BuildingKind.Lookout);
            if (building.Kind == BuildingKind.Jetty)
            {
                JettyActions(world, building);
                return;
            }

            if (!produces)
            {
                Text("sheet-text", _sheet, building.Kind switch
                {
                    BuildingKind.Longhouse => "Ger plats åt åtta bybor.",
                    BuildingKind.Cottage => "Ger plats åt fyra bybor.",
                    BuildingKind.Storehouse => "Rymmer 150 av varje vara. Byggnader nära förrådet får fram mer.",
                    BuildingKind.Jetty => "Härifrån kan sjövägar och expeditioner utgå.",
                    _ => "Kartläggning kostar en fjärdedel mindre Utforskning.",
                });
                return;
            }

            if (building.Staffed && !building.WorkdayToday)
            {
                AddAction("Dagsverke", $"{Workday.Cost} Gryningskraft · 50 % mer i dag", world.Available(Force.DawnPower) >= Workday.Cost, () => Run(new Workday(building.Site.Id)));
            }
            else if (building.WorkdayToday)
            {
                Text("sheet-text", _sheet, "Dagsverke i dag: 50 % mer.");
            }

            if (building.Level == 1 && !world.Projects.Any(p => p.Site == building.Site && p.Kind == ProjectKind.Upgrade))
            {
                AddAction("Uppgradera till nivå 2", Texts.Cost(BuildingSpec.Upgrade), Affordable(world, BuildingSpec.Upgrade), () => Run(new Upgrade(building.Site.Id)));
            }

            if (!building.HasRoad && DepotDistance(world, building.Site) > 1
                && !world.Projects.Any(p => p.Site == building.Site && p.Kind == ProjectKind.Road))
            {
                AddAction("Väg till förrådet", "2 sten och 4 h arbete per steg · mer når fram", true, () => Run(new BuildRoad(building.Site.Id)));
            }
        }

        /// <summary>The jetty: expeditions from home, the sea route, and Nyön's goods waiting for the ship.</summary>
        private void JettyActions(World world, Building building)
        {
            if (building.Site.Island == IslandId.Nyon)
            {
                var waiting = Enum.GetValues(typeof(Resource)).Cast<Resource>()
                    .Where(r => world.Stock(IslandId.Nyon, r) > 0)
                    .Select(r => $"{world.Stock(IslandId.Nyon, r)} {Texts.Name(r)}")
                    .ToList();
                Text("sheet-text", _sheet, "Skeppen tar Nyöns varor hem, 40 per dygn.");
                Text("sheet-text", _sheet, waiting.Count == 0 ? "Inget väntar på skeppen just nu." : "Väntar på skeppen: " + string.Join(", ", waiting) + ".");
                return;
            }

            if (world.ExpeditionHoursLeft > 0)
            {
                Text("sheet-text", _sheet, $"Expeditionen är ute. Om {world.ExpeditionHoursLeft} timmar med vakna bybor är den tillbaka.");
            }
            else if (!world.IsRevealed("N0"))
            {
                int cost = SendExpedition.Cost(world);
                AddAction("Skicka expedition", $"{cost} Utforskning · 32 h med vakna bybor · hittar land bortom horisonten",
                    world.Available(Force.Exploration) >= cost, () => Run(new SendExpedition()));
            }
            else
            {
                Text("sheet-text", _sheet, world.SeaRouteOpen
                    ? "Sjövägen till Nyön är öppen. Skeppen tar hem 40 varor per dygn."
                    : "Nyön är hittad. Bygg en brygga där, så öppnar sjövägen.");
            }
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

        private void AddAction(string title, string cost, bool available, System.Action onClick)
        {
            var button = new Button(onClick);
            button.AddToClassList("action");
            if (!available)
            {
                button.AddToClassList("action-unavailable");
            }

            Text("action-title", button, title);
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

        private static int DepotDistance(World world, Site site) => world.Buildings
            .Where(b => b.Kind is BuildingKind.Storehouse or BuildingKind.Jetty)
            .Select(b => b.Site.DistanceTo(site))
            .DefaultIfEmpty(int.MaxValue)
            .Min();

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
