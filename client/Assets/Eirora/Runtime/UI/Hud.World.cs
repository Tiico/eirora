using System;
using System.Collections.Generic;
using System.Linq;
using Eirora.Client.Look;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Time;
using UnityEngine;
using UnityEngine.UIElements;
using MapTerrain = Eirora.Simulation.Map.Terrain;

namespace Eirora.Client.UI
{
    /// <summary>
    /// The interface that lives on the island (ui-prototypes.html, direction C): a bubble over each building that has
    /// something to say, the compass where the fog can be lifted, a ring of choices around the selected tile with the
    /// cost of each as a chip, the late carrier where goods wait, and the arc of the night. Each is anchored to a point
    /// in the world and follows the camera; the cards stay for what needs more words, behind the ring's last choice.
    /// </summary>
    public sealed partial class Hud
    {
        // The ring's choices and the bubbles, in the panel's reference pixels (1080 wide).
        private const float ChoiceSize = 156f;
        private const float RingRadius = 250f;
        private const float BubbleSize = 96f;
        private const float CaptionWidth = 760f;
        private const int MostChoices = 7;

        private readonly VisualElement _islandLayer;
        private readonly VisualElement _bubbles;
        private readonly VisualElement _ring;
        private readonly List<Anchor> _anchors = new();
        private VisualElement? _caption;
        private Camera? _camera;
        private Pictograms? _pictograms;
        private bool _unfolded;
        private int _chosen = -1;
        private IReadOnlyList<Hex> _wayBehind = Array.Empty<Hex>();

        /// <summary>
        /// A choice on the ring around the selected tile: what it is, what it costs as chips and as words, and what it does
        /// when chosen twice. Tapping it once shows its words under the ring; tapping it again performs it.
        /// </summary>
        public sealed class Choice
        {
            public Choice(string title, string costText, bool available, System.Action perform, IReadOnlyList<CostChip> costs, string? effect = null, BuildingKind? building = null, GlyphKind? glyph = null, Force? force = null)
            {
                Title = title;
                CostText = costText;
                Available = available;
                Perform = perform;
                Costs = costs;
                Effect = effect;
                Building = building;
                Glyph = glyph;
                Force = force;
            }

            public string Title { get; }

            /// <summary>What it does, in the words the cards use, or null when the title says it.</summary>
            public string? Effect { get; }

            /// <summary>The cost in the words the cards use.</summary>
            public string CostText { get; }

            public IReadOnlyList<CostChip> Costs { get; }

            public bool Available { get; }

            public System.Action Perform { get; }

            /// <summary>The building it builds, shown as its picture.</summary>
            public BuildingKind? Building { get; }

            public GlyphKind? Glyph { get; }

            /// <summary>The force it spends, shown as the force's shape when it has no picture or glyph.</summary>
            public Force? Force { get; }
        }

        /// <summary>One part of a cost: a good, a force or hours, with the amount and whether it is lacking.</summary>
        public readonly struct CostChip
        {
            public CostChip(string amount, bool lacking, Resource? resource = null, Force? force = null, GlyphKind? glyph = null)
            {
                Amount = amount;
                Lacking = lacking;
                Resource = resource;
                Force = force;
                Glyph = glyph;
            }

            public string Amount { get; }

            public bool Lacking { get; }

            public Resource? Resource { get; }

            public Force? Force { get; }

            public GlyphKind? Glyph { get; }
        }

        /// <summary>Follows this camera: the bubbles and the ring are placed over the island it sees.</summary>
        public void UseCamera(Camera camera) => _camera = camera;

        /// <summary>Shows the buildings as their own pictures on the ring.</summary>
        public void UsePictograms(Pictograms pictograms) => _pictograms = pictograms;

        /// <summary>
        /// The way from the selected building to its depot while its carriers are behind, for the island to mark; empty
        /// otherwise (TASK-043).
        /// </summary>
        public IReadOnlyList<Hex> WayBehind => _wayBehind;

        /// <summary>Unfolds the card for the selected tile: every building that can stand there, or the building's own.</summary>
        public void ShowMore()
        {
            _unfolded = true;
            _chosen = -1;
            ShowSheet();
        }

        /// <summary>The choices on the ring, in order, for the tests and the tour.</summary>
        public IReadOnlyList<Choice> Choices => _ring.Query<Button>(className: "choice").ToList().Select(b => (Choice)b.userData).ToList();

        /// <summary>Places what is anchored to the island where the camera now shows it; called every frame.</summary>
        public void FollowCamera()
        {
            if (_camera is null || _root.panel is null)
            {
                return;
            }

            // The camera's view covers the panel, so a point's share of the view is its share of the panel.
            float width = _root.resolvedStyle.width, height = _root.resolvedStyle.height;
            if (!(width > 0f) || !(height > 0f))
            {
                return;
            }

            foreach (var anchor in _anchors)
            {
                var view = _camera.WorldToViewportPoint(anchor.Point);
                if (view.z < 0f)
                {
                    anchor.Element.style.display = DisplayStyle.None;
                    continue;
                }

                var at = new Vector2(view.x * width, (1f - view.y) * height);
                if (anchor.Margin > 0f)
                {
                    // The ring stays whole on the screen and clear of the rail, wherever the tile is; the glow on the island
                    // still marks the tile, and its words stay within the screen's sides.
                    at.x = Mathf.Clamp(at.x, anchor.Margin + 170f, width - anchor.Margin - 90f);
                    at.y = Mathf.Clamp(at.y, anchor.Margin + 120f, height - anchor.Margin - 160f);
                    if (_caption is not null)
                    {
                        _caption.style.left = Mathf.Clamp(-CaptionWidth / 2f, 24f - at.x, width - 24f - CaptionWidth - at.x);
                    }
                }

                anchor.Element.style.display = DisplayStyle.Flex;
                anchor.Element.style.left = at.x - (anchor.Width / 2f);
                anchor.Element.style.top = at.y - anchor.Height + anchor.Lift;
            }
        }

        /// <summary>
        /// The bubbles while the villagers are awake: what each building makes, the sign of one that stands still, the work
        /// on a site, a depot's store and the compass where the fog can be lifted. At night the island rests, and so do they.
        /// </summary>
        private void ShowWorld(World world)
        {
            _anchors.RemoveAll(a => a.Element != _ring);
            _bubbles.Clear();
            if (world.Phase != Phase.Active)
            {
                return;
            }

            var map = world.Map;
            foreach (var building in world.Buildings)
            {
                var diagnosis = _diagnostics?.At(building.Tile);
                var spec = world.Content.For(building.Kind);
                if (Logistics.IsDepot(building.Kind))
                {
                    StoreBubble(world, building);
                }
                else if (diagnosis?.StandsStill == true)
                {
                    SignBubble(building.Tile);
                }
                else if (building.Paused)
                {
                    Bubble(building.Tile, new Glyph(GlyphKind.Pause, 48f));
                }
                else if (spec.Recipe is { Product: Resource product } && diagnosis is not null)
                {
                    Bubble(building.Tile, new ResourceIcon(product, 54f));
                }
            }

            // A site under construction: the hammer, and how far the work has come as a ring around it.
            foreach (var project in world.Projects.Where(p => p.Kind != ProjectKind.Road))
            {
                var spec = project.Kind == ProjectKind.Upgrade ? world.Content.Upgrade : world.Content.For(project.Building!.Value);
                float done = project.ByHand
                    ? 1f - (project.DaysLeft / (float)Mathf.Max(spec.HandBuildDays, 1))
                    : 1f - (project.WorkLeft / (float)Mathf.Max(spec.WorkHours * 1000L, 1L));
                var bubble = Bubble(project.Tile, new Glyph(GlyphKind.Hammer, 48f));
                bubble.Add(new ProgressRing(Mathf.Clamp01(done), Palette.Grass));
            }

            // The compass stands in the fog nearest the village where the next survey lifts it (D-96).
            if (Survey.NextCost(world) is int cost && Nearest(world, map, world.Map.Start) is Hex edge)
            {
                var compass = new Button(() => Run(new Survey()));
                compass.AddToClassList("compass");
                compass.name = "Kartlägg";
                compass.Add(new ForceIcon(Force.Exploration, 84f));
                compass.Add(Chip(new CostChip(cost.ToString(System.Globalization.CultureInfo.InvariantCulture), world.Available(Force.Exploration) < cost, force: Force.Exploration)));
                _bubbles.Add(compass);
                _anchors.Add(new Anchor(compass, Ground(world, edge), 150f, 150f, lift: 75f));
            }

            // The late carrier on the way between the selected building and its depot, where the sign says why (TASK-043).
            if (_wayBehind.Count > 0)
            {
                var late = new VisualElement();
                late.AddToClassList("late");
                late.Add(new Glyph(GlyphKind.Carrier, 66f, Palette.FaluRed));
                var clock = new Glyph(GlyphKind.Clock, 42f, Palette.FaluRed);
                clock.AddToClassList("late-clock");
                late.Add(clock);
                _bubbles.Add(late);
                _anchors.Add(new Anchor(late, Ground(world, _wayBehind[_wayBehind.Count / 2]) + (Vector3.up * 0.3f), 120f, 120f, lift: 60f));
            }
        }

        private Button Bubble(Hex tile, VisualElement content, float width = BubbleSize)
        {
            var bubble = new Button(() => Select(tile));
            bubble.AddToClassList("bubble");
            bubble.name = tile.ToString();
            bubble.Add(content);
            var tail = new VisualElement();
            tail.AddToClassList("bubble-tail");
            bubble.Add(tail);
            _bubbles.Add(bubble);
            _anchors.Add(new Anchor(bubble, Above(_world, tile), width, BubbleSize, lift: -14f));
            return bubble;
        }

        /// <summary>The sign of a building that stands still, where the island draws it too, so that the two are one.</summary>
        private void SignBubble(Hex tile)
        {
            var bubble = Bubble(tile, new StandingStillIcon(64f));
            bubble.AddToClassList("bubble-sign");
        }

        /// <summary>A depot's bubble: the goods its island's store holds most of, each as its shape and how full it is.</summary>
        private void StoreBubble(World world, Building depot)
        {
            int capacity = Mathf.Max(world.StorageUnits(depot.Island), 1);
            var goods = Enum.GetValues(typeof(Resource)).Cast<Resource>()
                .Where(r => Met(world, r) && world.Stock(depot.Island, r) > 0)
                .OrderByDescending(r => world.Stock(depot.Island, r))
                .ThenBy(r => r)
                .Take(4)
                .ToList();
            var shelf = new VisualElement();
            shelf.AddToClassList("shelf");
            foreach (var good in goods)
            {
                var stock = new VisualElement();
                stock.AddToClassList("stock");
                var bar = new VisualElement();
                bar.AddToClassList("stock-bar");
                var fill = new VisualElement();
                fill.AddToClassList("stock-fill");
                fill.style.height = Length.Percent(Mathf.Clamp(world.Stock(depot.Island, good) * 100f / capacity, 6f, 100f));
                bar.Add(fill);
                stock.Add(bar);
                stock.Add(new ResourceIcon(good, 36f));
                shelf.Add(stock);
            }

            if (goods.Count == 0)
            {
                shelf.Add(new Glyph(GlyphKind.House, 48f));
            }

            var bubble = Bubble(depot.Tile, shelf, goods.Count == 0 ? BubbleSize : 36f + (goods.Count * 66f));
            bubble.AddToClassList("bubble-wide");
        }

        /// <summary>
        /// The ring around the selected tile: what can stand on a site, a building's quick choices, or the fog's lifting,
        /// with the words of the chosen one under it. The last choice unfolds the card.
        /// </summary>
        private void ShowRing(World world)
        {
            HideRing();
            if (_open == Sheet.Tile && _tile is Hex tile)
            {
                var (choices, title, text) = RingFor(world, tile);
                Ring(world, tile, choices, title, text);
            }
            else if (_open == Sheet.Fog && _fog is Hex fog)
            {
                var rejection = world.CheckLiftFog(fog);
                var choices = rejection == Rejection.None
                    ? new[] { new Choice("Lyft dimman", $"{LiftFog.Cost} Utforskning", world.Phase == Phase.Active && world.Available(Force.Exploration) >= LiftFog.Cost, () => Run(new LiftFog(fog.Q, fog.R)), ForceChips(world, Force.Exploration, LiftFog.Cost), "Utforskningen lyfter dimman och visar vad som finns här.", force: Force.Exploration) }
                    : Array.Empty<Choice>();
                Ring(world, fog, choices, "Dimma", rejection == Rejection.None ? null : Texts.Reason(rejection));
            }
        }

        private void Ring(World world, Hex tile, IReadOnlyList<Choice> choices, string title, string? text)
        {
            _ring.style.display = DisplayStyle.Flex;
            var close = new Button(() => Select(null)) { text = "×" };
            close.AddToClassList("ring-centre");
            _ring.Add(close);

            int count = choices.Count;
            float radius = count > 5 ? RingRadius + 40f : RingRadius;
            for (int i = 0; i < count; i++)
            {
                var choice = choices[i];
                int index = i;
                float angle = (-90f + (i * 360f / Mathf.Max(count, 1))) * Mathf.Deg2Rad;
                var button = new Button(() => Choose(index, choice));
                button.AddToClassList("choice");
                if (!choice.Available)
                {
                    button.AddToClassList("choice-unavailable");
                }

                if (index == _chosen)
                {
                    button.AddToClassList("choice-chosen");
                }

                button.name = choice.Title;
                button.userData = choice;
                button.style.left = (Mathf.Cos(angle) * radius) - (ChoiceSize / 2f);
                button.style.top = (Mathf.Sin(angle) * radius) - (ChoiceSize / 2f);
                button.Add(Picture(choice));
                if (choice.Costs.Count > 0)
                {
                    var chips = new VisualElement();
                    chips.AddToClassList("choice-chips");
                    foreach (var chip in choice.Costs.Take(3))
                    {
                        chips.Add(Chip(chip));
                    }

                    button.Add(chips);
                }

                _ring.Add(button);
            }

            // The words: the chosen choice's, or the tile's own.
            var caption = new VisualElement();
            caption.AddToClassList("caption");
            caption.style.top = radius + (ChoiceSize / 2f) + 60f;
            var chosen = _chosen >= 0 && _chosen < count ? choices[_chosen] : null;
            Text("caption-title", caption, chosen?.Title ?? title);
            string? detail = chosen is null ? text : chosen.Effect ?? (chosen.Costs.Count == 0 ? null : chosen.CostText);
            if (detail is not null)
            {
                Text("caption-text", caption, detail);
            }

            if (chosen is not null)
            {
                if (chosen.Effect is not null && chosen.Costs.Count > 0)
                {
                    Text("caption-text", caption, chosen.CostText);
                }

                Text("caption-hint", caption, chosen.Available ? "Tryck igen" : "Går inte än");
            }

            _ring.Add(caption);
            _caption = caption;
            _anchors.Add(new Anchor(_ring, Ground(world, tile), 0f, 0f, lift: 0f, margin: radius + (ChoiceSize / 2f) + 20f));
            FollowCamera();
        }

        private void HideRing()
        {
            _ring.Clear();
            _caption = null;
            _anchors.RemoveAll(a => a.Element == _ring);
            _ring.style.display = DisplayStyle.None;
        }

        private void Choose(int index, Choice choice)
        {
            if (_chosen == index)
            {
                _chosen = -1;
                choice.Perform();
                return;
            }

            _chosen = index;
            ShowSheet();
        }

        /// <summary>The ring for a known tile: the buildings a site takes, a project's place in the queue, or a building's choices.</summary>
        private (IReadOnlyList<Choice> Choices, string Title, string? Text) RingFor(World world, Hex tile)
        {
            var uses = world.UsesOf(tile);
            string ground = Texts.Ground(world.Map, tile, uses) + (world.HasRoad(tile) ? " med väg" : string.Empty);
            var building = world.BuildingAt(tile);
            var project = world.Projects.FirstOrDefault(p => p.Tile == tile && p.Kind is ProjectKind.Build or ProjectKind.Road);
            if (building is not null)
            {
                return BuildingRing(world, building, ground);
            }

            var choices = new List<Choice>();
            if (project is { ByHand: true })
            {
                choices.Add(More());
                return (choices, $"{Texts.Name(project.Building!.Value)} byggs med handkraft", $"{ground} · {Texts.WorkLeft(project)} · står klar i gryningen dygn {world.StandsOnGameDay(project)}");
            }

            if (project is not null)
            {
                var worked = world.Projects.Where(p => !p.ByHand).ToList();
                int place = IndexOf(worked, project);
                if (place > 0)
                {
                    choices.Add(new Choice("Bygg först", "Lediga bybor går hit före de andra projekten · ingen kraft", true, () => Run(new QueueFirst(project.Kind, project.Tile)), Array.Empty<CostChip>(), glyph: GlyphKind.First));
                }

                choices.Add(More());
                string queue = place == 0 ? "Först i byggkön" : $"Nummer {place + 1} i byggkön";
                return (choices, project.Building is BuildingKind kind ? $"{Texts.Name(kind)} byggs" : "Väg byggs", $"{ground} · {Texts.WorkLeft(project)} · {queue}");
            }

            // What only this kind of tile allows; the buildings that any land takes wait on the card when the tile is
            // special for something, so that the ring stays few (D-79).
            var (special, anyLand, road) = Buildable(world, tile);
            var shown = special.Count > 0 ? special : anyLand;
            foreach (var kind in shown.Take(MostChoices - 1))
            {
                var spec = world.Content.For(kind);
                choices.Add(new Choice(Texts.Name(kind), Texts.Cost(spec), Affordable(world, spec, tile), () => Run(new Build(kind, tile)), BuildChips(world, spec, tile), Texts.Effect(kind, spec), building: kind));
            }

            if (special.Count == 0 && road && choices.Count < MostChoices - 1)
            {
                choices.Add(RoadChoice(world, tile));
            }

            if (special.Count > 0 || anyLand.Count > MostChoices - 1 || road && choices.Count >= MostChoices - 1)
            {
                choices.Add(More());
            }

            string? note = null;
            if (shown.Count == 0 && (uses & TileUse.Crest) != 0)
            {
                note = "Här ska Vårdkasen stå.";
            }
            else if (shown.Count == 0 && uses != TileUse.None)
            {
                bool sailing = world.Buildings.Any(b => b.Kind == BuildingKind.Jetty && b.Island == world.HomeIsland);
                note = sailing ? "Bygg en brygga på ön först, så når byggarna hit." : "Bygg en brygga på Hemön först, så seglar byggarna hit.";
            }
            else
            {
                note = Texts.Uses(Unlocked(world, uses)) + (world.Map.TerrainAt(tile) == MapTerrain.Forest ? " " + Texts.Timber(world.TimberLeft(tile)) : string.Empty);
            }

            return (choices, ground, note);
        }

        private (IReadOnlyList<Choice> Choices, string Title, string? Text) BuildingRing(World world, Building building, string ground)
        {
            var spec = world.Content.For(building.Kind);
            var diagnosis = _diagnostics?.At(building.Tile);
            bool awake = world.Phase == Phase.Active;
            var choices = new List<Choice>();
            if (spec.Produces || Logistics.IsDepot(building.Kind))
            {
                if (building.Staffed && !building.WorkdayToday)
                {
                    string gives = spec.Produces ? "50 % mer i dag" : "bärarna bär hälften mer i dag";
                    choices.Add(new Choice("Arbeta extra", $"{Workday.Cost} Ork · {gives}", awake && world.Available(Force.DawnPower) >= Workday.Cost, () => Run(new Workday(building.Tile)), ForceChips(world, Force.DawnPower, Workday.Cost), gives, force: Force.DawnPower));
                }
            }

            if (spec.Produces && building.Level == 1 && !world.Projects.Any(p => p.Tile == building.Tile && p.Kind == ProjectKind.Upgrade))
            {
                var upgrade = world.Content.Upgrade;
                choices.Add(new Choice("Uppgradera till nivå 2", Texts.Cost(upgrade), awake && Affordable(world, upgrade, building.Tile), () => Run(new Upgrade(building.Tile)), BuildChips(world, upgrade, building.Tile), glyph: GlyphKind.Upgrade));
            }

            int tiles = spec.Produces ? world.RoadToDepot(building.Tile).Count : 0;
            if (tiles > 0)
            {
                string gives = diagnosis?.Hindrances.OfType<CarriersBehind>().FirstOrDefault() is { } behind && diagnosis.Product is Resource product && behind.WithRoadPerHour > behind.PerHour
                    ? Texts.RoadGives(behind, product)
                    : "bärarna går dubbelt så fort";
                var cost = new Dictionary<Resource, int> { [Resource.Stone] = Logistics.RoadStone * tiles };
                choices.Add(new Choice("Väg till förrådet", $"{Logistics.RoadStone * tiles} sten och {Logistics.RoadHours * tiles} h arbete · {gives}", awake && world.CanAfford(cost, building.Tile), () => Run(new BuildRoad(building.Tile)),
                    new[] { new CostChip((Logistics.RoadStone * tiles).ToString(System.Globalization.CultureInfo.InvariantCulture), !world.CanAfford(cost, building.Tile), resource: Resource.Stone), Hours(Logistics.RoadHours * tiles) }, gives, glyph: GlyphKind.Road));
            }

            if (building.Kind == BuildingKind.Jetty && building.Island == world.HomeIsland && world.ExpeditionHoursLeft == 0 && world.ExpeditionFinds is Hex finds && !world.IsKnown(finds))
            {
                int cost = SendExpedition.Cost(world);
                choices.Add(new Choice("Skicka expedition", $"{cost} Utforskning · {PrototypeIsland.ExpeditionHours} h med vakna bybor · hittar land bortom horisonten", awake && world.Available(Force.Exploration) >= cost, () => Run(new SendExpedition()),
                    ForceChips(world, Force.Exploration, cost).Append(Hours(PrototypeIsland.ExpeditionHours)).ToList(), "Hittar land bortom horisonten", glyph: GlyphKind.Boat));
            }

            if (spec.Staff > 0 && building.Paused)
            {
                choices.Add(new Choice("Återuppta", "Bemannas igen på sin plats i bemanningen · ingen kraft", awake, () => Run(new ResumeBuilding(building.Tile)), Array.Empty<CostChip>(), glyph: GlyphKind.Resume));
            }
            else if (spec.Staff > 0 && spec.Pausable)
            {
                choices.Add(new Choice("Pausa", $"{Capital(Texts.Villagers(spec.Staff))} bygger i stället · ingen kraft", awake, () => Run(new PauseBuilding(building.Tile)), Array.Empty<CostChip>(), glyph: GlyphKind.Pause));
            }

            choices.Add(More());
            string title = Texts.Name(building.Kind) + (building.Level == 2 ? " · nivå 2" : string.Empty);
            string? state = diagnosis is null ? Texts.Resting(building, spec) : Texts.Status(diagnosis, spec);
            string text = state is null ? ground : $"{ground} · {state}";
            if (diagnosis is not null && diagnosis.Hindrances.Count > 0)
            {
                text += " · " + Texts.Hindrance(diagnosis.Hindrances[0], diagnosis.Product, building.Island == world.HomeIsland);
            }

            return (choices, title, text);
        }

        private Choice More() => new("Mer", "Allt om platsen på ett kort", true, ShowMore, Array.Empty<CostChip>(), glyph: GlyphKind.More);

        private Choice RoadChoice(World world, Hex tile) =>
            new("Lägg väg", $"{Logistics.RoadStone} sten och {Logistics.RoadHours} h arbete per ruta · bärarna går dubbelt så fort", world.Stock(Resource.Stone) >= Logistics.RoadStone, () =>
            {
                LayRoads();
                LayRoad(world, tile);
            }, new[] { new CostChip(Logistics.RoadStone.ToString(System.Globalization.CultureInfo.InvariantCulture), world.Stock(Resource.Stone) < Logistics.RoadStone, resource: Resource.Stone), Hours(Logistics.RoadHours) }, "Bärarna går dubbelt så fort", glyph: GlyphKind.Road);

        /// <summary>What can stand on a tile: what only its kind allows, what any land takes, and whether a road can lie there.</summary>
        private static (List<BuildingKind> Special, List<BuildingKind> AnyLand, bool Road) Buildable(World world, Hex tile)
        {
            var buildable = world.Content.Buildings.Where(kind => world.CheckBuild(kind, tile) == Rejection.None).ToList();
            var special = buildable.Where(kind => world.Content.For(kind).Requirement != TileUse.Land).ToList();
            return (special, buildable.Except(special).ToList(), world.CheckRoad(tile) == Rejection.None);
        }

        /// <summary>A building's cost as chips: each good, the Build Power and the work, with what is lacking marked.</summary>
        private static IReadOnlyList<CostChip> BuildChips(World world, BuildingSpec spec, Hex tile)
        {
            var chips = spec.Cost
                .OrderBy(c => c.Key)
                .Select(c => new CostChip(c.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), !world.CanAfford(new Dictionary<Resource, int> { [c.Key] = c.Value }, tile), resource: c.Key))
                .ToList();
            if (spec.BuildPower > 0)
            {
                chips.Add(new CostChip(spec.BuildPower.ToString(System.Globalization.CultureInfo.InvariantCulture), world.Available(Force.BuildPower) < spec.BuildPower, force: Force.BuildPower));
            }

            chips.Add(Hours(spec.WorkHours));
            return chips;
        }

        private static IReadOnlyList<CostChip> ForceChips(World world, Force force, int cost) =>
            new[] { new CostChip(cost.ToString(System.Globalization.CultureInfo.InvariantCulture), world.Available(force) < cost, force: force) };

        private static CostChip Hours(int hours) => new($"{hours} h", false, glyph: GlyphKind.Clock);

        private VisualElement Picture(Choice choice)
        {
            if (choice.Building is BuildingKind kind && _pictograms?.Of(kind) is Texture picture)
            {
                var image = new VisualElement();
                image.AddToClassList("choice-picture");
                image.style.backgroundImage = picture is RenderTexture render ? Background.FromRenderTexture(render) : Background.FromTexture2D((Texture2D)picture);
                image.pickingMode = PickingMode.Ignore;
                return image;
            }

            if (choice.Force is Force force && choice.Glyph is null)
            {
                return new ForceIcon(force, 84f);
            }

            return new Glyph(choice.Glyph ?? GlyphKind.House, 84f);
        }

        private static VisualElement Chip(CostChip cost)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            if (cost.Lacking)
            {
                chip.AddToClassList("chip-lack");
            }

            if (cost.Resource is Resource resource)
            {
                chip.Add(new ResourceIcon(resource, 36f));
            }
            else if (cost.Force is Force force)
            {
                chip.Add(new ForceIcon(force, 36f));
            }
            else
            {
                chip.Add(new Glyph(cost.Glyph ?? GlyphKind.Clock, 36f));
            }

            Text("chip-amount", chip, cost.Amount);
            return chip;
        }

        /// <summary>The known fog tile nearest a point where the fog can be lifted, for the compass.</summary>
        private static Hex? Nearest(World world, WorldMap map, Hex from)
        {
            Hex? nearest = null;
            int best = int.MaxValue;
            for (int i = 0; i < map.Disc.Count; i++)
            {
                var hex = map.Disc[i];
                if (!world.IsKnown(hex) && world.CanLiftFog(hex))
                {
                    int distance = from.DistanceTo(hex);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = hex;
                    }
                }
            }

            return nearest;
        }

        private static Vector3 Ground(World world, Hex hex)
        {
            float height = world.Map.Contains(hex) ? Models.Height(IslandMap.Look(world.Map.TerrainAt(hex))) : 0f;
            return hex.Center() + (Vector3.up * Mathf.Max(height, 0f));
        }

        private static Vector3 Above(World? world, Hex hex) =>
            world is null ? hex.Center() : Ground(world, hex) + (Vector3.up * StandingStill.Height);

        /// <summary>The night: an arc from the moon to the sun with how far the rest has come, and when the world opens.</summary>
        private VisualElement NightArc(World world)
        {
            int minute = (int)(world.Now.LocalSeconds / 60 % 1440);
            int elapsed = (minute - world.RestSchedule.StartMinuteOfDay + 1440) % 1440;
            float progress = Mathf.Clamp01(elapsed / (float)Mathf.Max(world.RestSchedule.DurationMinutes, 1));
            var night = new VisualElement();
            night.AddToClassList("night-arc");
            night.Add(new Arc(progress));
            Text("night-time", night, Clock(RestEnd(world)));
            return night;
        }

        private sealed class Anchor
        {
            public Anchor(VisualElement element, Vector3 point, float width, float height, float lift, float margin = 0f)
            {
                Element = element;
                Point = point;
                Width = width;
                Height = height;
                Lift = lift;
                Margin = margin;
            }

            public VisualElement Element { get; }

            public Vector3 Point { get; }

            public float Width { get; }

            public float Height { get; }

            /// <summary>How far below the point the element's bottom edge lies, in panel pixels.</summary>
            public float Lift { get; }

            /// <summary>How far from the panel's edges the point is kept, so that the element stays whole on the screen.</summary>
            public float Margin { get; }
        }

        /// <summary>How far the work on a site has come, as a ring around its bubble.</summary>
        private sealed class ProgressRing : VisualElement
        {
            private readonly float _done;
            private readonly Color _colour;

            public ProgressRing(float done, Color colour)
            {
                _done = done;
                _colour = colour;
                AddToClassList("progress-ring");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float w = contentRect.width, h = contentRect.height;
                var centre = new Vector2(w / 2f, h / 2f);
                float radius = (Mathf.Min(w, h) / 2f) - 4f;
                p.lineWidth = 8f;
                p.lineCap = LineCap.Round;
                p.strokeColor = new Color(Palette.Text.r, Palette.Text.g, Palette.Text.b, 0.15f);
                p.BeginPath();
                p.Arc(centre, radius, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();
                if (_done > 0f)
                {
                    p.strokeColor = _colour;
                    p.BeginPath();
                    p.Arc(centre, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + (360f * _done)));
                    p.Stroke();
                }
            }
        }

        /// <summary>The night's arc: the moon at its start, the sun at its end, and the rest's progress along it.</summary>
        private sealed class Arc : VisualElement
        {
            private readonly float _progress;

            public Arc(float progress)
            {
                _progress = progress;
                AddToClassList("arc");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float w = contentRect.width, h = contentRect.height;
                float radius = (w / 2f) - 40f;
                var centre = new Vector2(w / 2f, h - 24f);
                p.lineWidth = 7f;
                p.lineCap = LineCap.Round;
                p.strokeColor = new Color(Palette.RuneGlow.r, Palette.RuneGlow.g, Palette.RuneGlow.b, 0.3f);
                p.BeginPath();
                p.Arc(centre, radius, Angle.Degrees(180f), Angle.Degrees(360f));
                p.Stroke();
                float end = 180f + (180f * _progress);
                p.strokeColor = Palette.RuneGlow;
                p.BeginPath();
                p.Arc(centre, radius, Angle.Degrees(180f), Angle.Degrees(end));
                p.Stroke();
                float a = end * Mathf.Deg2Rad;
                p.fillColor = new Color(232 / 255f, 238 / 255f, 248 / 255f);
                Glyph.Disc(p, centre + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius), 14f);
            }
        }
    }
}
