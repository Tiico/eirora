using System;
using System.Collections.Generic;
using System.Linq;
using Eirora.Client.Look;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Events;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using MapTerrain = Eirora.Simulation.Map.Terrain;

namespace Eirora.Client.UI
{
    /// <summary>
    /// The prototype's Swedish texts in one place, ready to move into the localisation system (UX-06).
    /// Tone: calm and warm, never guilt (ux-journeys.md).
    /// </summary>
    public static class Texts
    {
        // The eight points of the compass, counter-clockwise from east as the map lies on the screen, north up.
        private static readonly string[] s_compass = { "öster", "nordost", "norr", "nordväst", "väster", "sydväst", "söder", "sydost" };

        public static string Name(BuildingKind kind) => kind switch
        {
            BuildingKind.Longhouse => "Långhus",
            BuildingKind.Cottage => "Stuga",
            BuildingKind.Storehouse => "Förråd",
            BuildingKind.FishingHut => "Fiskebod",
            BuildingKind.Field => "Åker",
            BuildingKind.Woodcutter => "Skogshuggare",
            BuildingKind.SheepFarm => "Fårgård",
            BuildingKind.Quarry => "Stenbrott",
            BuildingKind.Sawmill => "Sågverk",
            BuildingKind.Weavery => "Väveri",
            BuildingKind.Jetty => "Brygga",
            BuildingKind.Lookout => "Utkikstorn",
            BuildingKind.Farmstead => "Bondgård",
            BuildingKind.HuntingLodge => "Jaktstuga",
            BuildingKind.Tannery => "Garveri",
            BuildingKind.CraftsmensHouse => "Hantverkargård",
            BuildingKind.Apiary => "Bigård",
            BuildingKind.Brewery => "Bryggeri",
            BuildingKind.Meadery => "Mjöderi",
            BuildingKind.TarKiln => "Tjärdal",
            BuildingKind.Shipyard => "Varv",
            _ => kind.ToString(),
        };

        public static string Name(Resource resource) => resource switch
        {
            Resource.Fish => "fisk",
            Resource.Grain => "säd",
            Resource.Timber => "timmer",
            Resource.Planks => "plankor",
            Resource.Stone => "sten",
            Resource.Wool => "ull",
            Resource.Wadmal => "tyg",
            Resource.Game => "vilt",
            Resource.Hide => "skinn",
            Resource.Honey => "honung",
            Resource.Mead => "mjöd",
            Resource.Ale => "öl",
            Resource.Tar => "tjära",
            _ => resource.ToString(),
        };

        /// <summary>A population tier, as many: "torpare", "bönder", "hantverkare" (GLOSSARY: Befolkningsnivå).</summary>
        public static string Name(PopulationTier tier) => tier switch
        {
            PopulationTier.Crofters => "torpare",
            PopulationTier.Farmers => "bönder",
            PopulationTier.Craftsmen => "hantverkare",
            _ => tier.ToString(),
        };

        /// <summary>A population tier, as one: "torpare", "bonde", "hantverkare".</summary>
        public static string One(PopulationTier tier) => tier == PopulationTier.Farmers ? "bonde" : Name(tier);

        /// <summary>A population tier in the definite form, as a sentence begins with it: "torparna", "bönderna", "hantverkarna".</summary>
        public static string Definite(PopulationTier tier) => tier switch
        {
            PopulationTier.Crofters => "torparna",
            PopulationTier.Farmers => "bönderna",
            PopulationTier.Craftsmen => "hantverkarna",
            _ => tier.ToString(),
        };

        /// <summary>A need by itself: "mat", "kläder", "dryck".</summary>
        public static string NeedName(Need need) => need switch
        {
            Need.Food => "mat",
            Need.Clothes => "kläder",
            Need.Drink => "dryck",
            _ => need.ToString(),
        };

        /// <summary>A need, as the sheets name it, with the goods that meet it: "kläder (tyg eller skinn)".</summary>
        public static string Name(Need need, ContentCatalog content) =>
            $"{NeedName(need)} ({string.Join(" eller ", content.For(need).Goods.Select(g => Name(g)))})";

        /// <summary>A tier's villagers and homes on an island, for its homes' sheet: "Hemön har 4 bönder i bostäder för 6."</summary>
        public static string TierHomes(World world, IslandSettlement settlement, PopulationTier tier)
        {
            string island = Capital(IslandName(world, settlement.Island));
            int villagers = settlement.VillagersOf(tier);
            return villagers == 0
                ? $"{island} har inga {Name(tier)} än, bostäder för {Count(settlement.HousingOf(tier))}."
                : $"{island} har {villagers} {Name(tier)} i bostäder för {settlement.HousingOf(tier)}.";
        }

        /// <summary>A resource's name after a count: "1 planka" but "2 plankor"; the other resources do not change.</summary>
        public static string Name(Resource resource, long count) =>
            resource == Resource.Planks && count == 1 ? "planka" : Name(resource);

        public static string Name(Force force) => force switch
        {
            Force.Exploration => "Utforskning",
            Force.BuildPower => "Styrka",
            _ => "Ork",
        };

        /// <summary>What a tile is: its ground, or the crest, and whether it is rich.</summary>
        public static string Ground(WorldMap map, Hex tile, TileUse uses)
        {
            string ground = (uses & TileUse.Crest) != 0 ? "Toppen" : map.TerrainAt(tile) switch
            {
                MapTerrain.DeepSea => "Djupt hav",
                MapTerrain.ShallowSea => "Grunt vatten",
                MapTerrain.Beach => "Strand",
                MapTerrain.Forest => "Skog",
                MapTerrain.Farmland => "Åker",
                MapTerrain.Pasture => "Betesmark",
                MapTerrain.Mountain => "Berg",
                _ => "Äng",
            };
            return map.RichnessAt(tile) > 100 ? $"{ground}, rik" : ground;
        }

        /// <summary>The timber left on a forest tile, which woodcutters fell and which grows back.</summary>
        public static string Timber(long left) => left >= World.TimberPerForest
            ? $"{World.TimberPerForest} timmer att fälla."
            : $"{left} av {World.TimberPerForest} timmer kvar att fälla. Skogen växer tillbaka.";

        /// <summary>Goods as a list, such as "7 timmer och 2 sten", in the resources' order; null when there are none.</summary>
        public static string? Goods(Func<Resource, long> amount)
        {
            var goods = Enum.GetValues(typeof(Resource)).Cast<Resource>()
                .Where(r => amount(r) > 0)
                .Select(r => $"{amount(r)} {Name(r, amount(r))}")
                .ToList();
            return goods.Count == 0 ? null : List(goods);
        }

        /// <summary>Items joined as Swedish lists them: "a, b och c".</summary>
        public static string List(IEnumerable<string> items)
        {
            var all = items.ToList();
            return all.Count <= 1 ? string.Concat(all) : string.Join(", ", all.Take(all.Count - 1)) + " och " + all[all.Count - 1];
        }

        /// <summary>What a tile is good for, as a building site (D-79, economy-v1.md, section 3.1).</summary>
        public static string Uses(TileUse uses)
        {
            var parts = new List<string>();
            void Add(TileUse use, string name)
            {
                if ((uses & use) != 0)
                {
                    parts.Add(name);
                }
            }

            Add(TileUse.Land, "byggmark");
            Add(TileUse.Farmland, "åker");
            Add(TileUse.Pasture, "betesmark");
            Add(TileUse.Meadow, "äng");
            Add(TileUse.Forest, "skog att fälla");
            Add(TileUse.Mountain, "berg att bryta");
            Add(TileUse.FishingWater, "fiskevatten");
            Add(TileUse.HarborSpot, "hamnläge");
            Add(TileUse.Crest, "Vårdkasens plats");
            return parts.Count == 0 ? "Här kan inget byggas." : "Duger till " + List(parts) + ".";
        }

        public static string Cost(BuildingSpec spec)
        {
            var parts = spec.Cost.Select(c => $"{c.Value} {Name(c.Key, c.Value)}").ToList();
            if (spec.BuildPower > 0)
            {
                parts.Add($"{spec.BuildPower} Styrka");
            }

            parts.Add($"{spec.WorkHours} h arbete");
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// What building by hand costs (D-65): the materials and the game days until it stands, as the epoch's button says
        /// it, <c>Bygg med handkraft · 7 dygn</c>; that it takes no force stands beside the name.
        /// </summary>
        public static string HandCost(BuildingSpec spec) =>
            string.Join(" · ", spec.Cost.Select(c => $"{c.Value} {Name(c.Key, c.Value)}").Append($"{spec.HandBuildDays} dygn"));

        /// <summary>
        /// What a building does in short, as the table in economy-v1.md (section 3) says it: from its recipe or housing in
        /// the content catalog, and for the storehouse, the jetty and the lookout from the rules. Null for a building that
        /// does none of these.
        /// </summary>
        public static string? Effect(BuildingKind kind, BuildingSpec spec)
        {
            if (spec.Recipe is Recipe recipe)
            {
                return Effect(recipe);
            }

            if (spec.Housing > 0)
            {
                return spec.Houses == PopulationTier.Crofters ? $"+{spec.Housing} bostadsplatser" : $"+{spec.Housing} bostäder för {Name(spec.Houses)}";
            }

            if (spec.Knarrs is KnarrSpec knarrs)
            {
                return $"Knarrar: +{knarrs.UnitsPerGameDay} per dag på sjövägarna, högst {Count(knarrs.Most)}";
            }

            return kind switch
            {
                BuildingKind.Storehouse => "Lager 150 per vara",
                BuildingKind.Jetty => "Sjöväg och expeditioner",
                BuildingKind.Lookout => "25 % mindre Utforskning",
                _ => null,
            };
        }

        /// <summary>
        /// What the ships carry on a sea route a game day, as the jetty's sheet says it: "40 varor per dag", "60 varor per
        /// dag med två knarrar" (economy-v2.md, section 4).
        /// </summary>
        public static string Capacity(SeaRoute route) =>
            route.Knarrs == 0 ? $"{route.PerGameDay} varor per dag"
            : route.Knarrs == 1 ? $"{route.PerGameDay} varor per dag med en knarr"
            : $"{route.PerGameDay} varor per dag med {Count(route.Knarrs)} knarrar";

        /// <summary>
        /// The shipyard's knarrs, in sentences for its sheet: how many its island has, how far the next has come, and what
        /// keeps it from beginning one (economy-v2.md, section 4).
        /// </summary>
        public static IEnumerable<string> Knarrs(World world, Building shipyard, KnarrSpec knarr)
        {
            int knarrs = world.Knarrs(shipyard.Island);
            string island = IslandName(world, shipyard.Island);
            yield return knarrs == 0 ? $"{Capital(island)} har ingen knarr än."
                : knarrs == 1 ? $"{Capital(island)} har en knarr."
                : $"{Capital(island)} har {Count(knarrs)} knarrar.";

            int begun = world.Buildings.Count(b => b.Island == shipyard.Island && b.KnarrHoursLeft > 0);
            if (shipyard.KnarrHoursLeft > 0)
            {
                yield return shipyard.Staffed
                    ? $"Nästa knarr är klar om {shipyard.KnarrHoursLeft} timmar med vakna bybor."
                    : $"Knarren väntar på hantverkare. {shipyard.KnarrHoursLeft} timmars arbete är kvar.";
            }
            else if (knarrs + begun >= knarr.Most)
            {
                yield return "Sjövägen rymmer inte fler knarrar härifrån.";
            }
            else if (!shipyard.Staffed)
            {
                yield return $"Varvet behöver {Count(world.Content.For(shipyard.Kind).Staff)} hantverkare för att bygga.";
            }
            else
            {
                var lacking = knarr.Cost
                    .OrderBy(c => c.Key)
                    .Select(c => (Resource: c.Key, Units: c.Value - world.Stock(shipyard.Island, c.Key)))
                    .Where(c => c.Units > 0)
                    .Select(c => $"{c.Units} {Name(c.Resource, c.Units)}")
                    .ToList();
                yield return $"Nästa knarr väntar på {List(lacking)} till i lagret.";
            }

            if (!world.SeaRoutes.Any(r => r.From == shipyard.Island || shipyard.Island == world.HomeIsland))
            {
                yield return "Knarrarna bär först när en sjöväg går hit.";
            }
        }

        /// <summary>Whether a building that needs staff works, is paused or waits for villagers; null for one that needs none.</summary>
        public static string? Staffing(Building building, BuildingSpec spec) =>
            spec.Staff == 0 ? null : building.Paused ? "pausad" : building.Staffed ? "i arbete" : "väntar på bybor";

        /// <summary>What a project in the queue makes: a building, a level or a road on a tile.</summary>
        public static string Project(Project project) => project.Kind switch
        {
            ProjectKind.Upgrade => $"{Name(project.Building!.Value)} till nivå 2",
            ProjectKind.Road => "Väg",
            _ => Name(project.Building!.Value),
        };

        /// <summary>The work hours a project has left, rounded up, or when a building built by hand stands (D-65).</summary>
        public static string WorkLeft(Project project) => project.ByHand
            ? project.DaysLeft == 1 ? "klar i gryningen" : $"klar om {project.DaysLeft} dygn"
            : $"{(project.WorkLeft + 999) / 1000} h arbete kvar";

        /// <summary>A number of villagers: "en bybo", "två bybor".</summary>
        public static string Villagers(int count) => count == 1 ? "en bybo" : $"{Count(count)} bybor";

        /// <summary>A number of free villagers: "en ledig bybo", "tre lediga bybor".</summary>
        public static string FreeVillagers(int count) => count == 1 ? "en ledig bybo" : $"{Count(count)} lediga bybor";

        /// <summary>
        /// A settlement's villagers and homes, as its island's sheets and the report show it (TASK-050): "Nyön: 3 av 4
        /// bybor, en ledig".
        /// </summary>
        public static string Settlement(World world, IslandSettlement settlement)
        {
            string free = settlement.FreeVillagers switch
            {
                0 => "alla i arbete",
                1 => "en ledig",
                int count => $"{Count(count)} lediga",
            };
            string island = Capital(IslandName(world, settlement.Island));
            string tiers = Tiers(settlement) is string above ? $", varav {above}" : string.Empty;
            return settlement.Villagers == 0 ? $"{island}: inga bybor än, bostäder för {Count(settlement.Housing)}"
                : settlement.Villagers > settlement.Housing ? $"{island}: {settlement.Villagers} bybor{tiers} i bostäder för {settlement.Housing}, {free}"
                : $"{island}: {settlement.Villagers} av {settlement.Housing} bybor{tiers}, {free}";
        }

        /// <summary>The villagers of the tiers above the crofters, such as "4 bönder"; null when all are crofters.</summary>
        public static string? Tiers(IslandSettlement settlement)
        {
            var tiers = Enum.GetValues(typeof(PopulationTier)).Cast<PopulationTier>()
                .Where(t => t != PopulationTier.Crofters && settlement.VillagersOf(t) > 0)
                .Select(t => $"{settlement.VillagersOf(t)} {Name(t)}")
                .ToList();
            return tiers.Count == 0 ? null : List(tiers);
        }

        /// <summary>
        /// What a tier lacks and why no one rises to it, in sentences for its island's sheets (TASK-061, TASK-043); nothing
        /// before its step is unlocked, while it rises, or when the settlement is hungry, which its growth says already.
        /// </summary>
        public static IEnumerable<string> Rising(TierDiagnosis tier, ContentCatalog content)
        {
            string name = Name(tier.Tier);
            string below = Name((PopulationTier)((int)tier.Tier - 1));
            if (tier.Lacks is Need lacks)
            {
                yield return $"{Capital(Definite(tier.Tier))} saknar {Name(lacks, content)}. De förblir {name}, men inga fler blir {name} förrän det finns.";
                yield break;
            }

            // The building that houses the tier, such as a farmstead.
            string homes = content.Buildings.Where(k => content.For(k) is { Housing: > 0 } spec && spec.Houses == tier.Tier)
                .Select(k => Name(k).ToLowerInvariant()).FirstOrDefault() ?? "bostad";
            switch (tier.Rising)
            {
                case Simulation.Economy.Rising.LacksNeed when tier.Missing is Need missing:
                    yield return $"Med {Name(missing, content)} i lagret kan {below} bli {name} i gryningen.";
                    break;
                case Simulation.Economy.Rising.NoHousing when tier.Housing == 0:
                    yield return $"Med en {homes} kan {below} bli {name}.";
                    break;
                case Simulation.Economy.Rising.NoHousing:
                    yield return $"Alla bostäder för {name} är fulla. Med en {homes} till kan fler bli {name}.";
                    break;
            }
        }

        /// <summary>
        /// Why a settlement does not grow and who could come to staff its buildings, in sentences for its sheets
        /// (TASK-050, D-106); nothing when it grows and lacks no one.
        /// </summary>
        public static IEnumerable<string> Growth(SettlementDiagnosis settlement)
        {
            var village = settlement.Settlement;
            switch (settlement.Growth)
            {
                case Simulation.Economy.Growth.Empty:
                    yield return "Här bor inga bybor än. De flyttar hit över sjövägen, högst en per gryning i hela ögruppen.";
                    break;
                case Simulation.Economy.Growth.Hungry:
                    yield return $"Maten räcker inte: {settlement.Food} mat i lagret till {Villagers(village.Villagers)}. Byn växer inte, men ingen flyttar härifrån.";
                    if (!settlement.HasRoom)
                    {
                        yield return "Alla bostäder är fulla också. Med en stuga eller ett långhus kan fler flytta in.";
                    }

                    break;
                case Simulation.Economy.Growth.Full:
                    yield return "Alla bostäder är fulla. Med en stuga eller ett långhus kan fler flytta in.";
                    break;
            }

            if (settlement.MissingStaff == 0)
            {
                yield break;
            }

            string missing = $"Byggnaderna saknar {Villagers(settlement.MissingStaff)}.";
            yield return !settlement.HasRoom ? $"{missing} Det finns ingen bostad för fler."
                : settlement.FreeNearby == 0 ? $"{missing} Ingen ledig bybo finns på öarna som sjövägen når."
                : $"{missing} En ledig bybo flyttar hit över sjövägen vid gryningen, om ingen ö saknar fler.";
        }

        /// <summary>
        /// What a finished building that produces nothing does, in a sentence for its sheet: the housing the content
        /// catalog gives it, or what the rules let the storehouse and the lookout do. Null for a building that does none.
        /// </summary>
        public static string? Purpose(BuildingKind kind, BuildingSpec spec)
        {
            if (spec.Housing > 0 && spec.Houses != PopulationTier.Crofters)
            {
                return $"Ger plats åt {Count(spec.Housing)} {Name(spec.Houses)}. {Capital(Name((PopulationTier)((int)spec.Houses - 1)))} blir {Name(spec.Houses)} här när de får det de behöver.";
            }

            if (spec.Housing > 0)
            {
                return spec.Housing == 1 ? "Ger plats åt en bybo." : $"Ger plats åt {Count(spec.Housing)} bybor.";
            }

            if (spec.Knarrs is KnarrSpec knarrs)
            {
                return $"Bygger knarrar av {List(knarrs.Cost.OrderBy(c => c.Key).Select(c => $"{c.Value} {Name(c.Key, c.Value)}"))}, en i taget på {knarrs.WorkHours} timmar. Varje knarr bär {knarrs.UnitsPerGameDay} varor mer per dag "
                    + $"på sjövägarna till och från ön, högst {Count(knarrs.Most)} på varje sjöväg.";
            }

            return kind switch
            {
                BuildingKind.Storehouse => "Rymmer 150 av varje vara. Bärarna hämtar det byggnaderna närmast gör.",
                BuildingKind.Lookout => "Kartläggning och expeditioner kostar en fjärdedel mindre Utforskning.",
                _ => null,
            };
        }

        private static string Effect(Recipe recipe)
        {
            string product = Name(recipe.Product);
            string one = Name(recipe.Product, 1);
            if (recipe.Inputs.Count > 0)
            {
                string inputs = List(recipe.Inputs.Select(i => $"{i.PerProduct} {Name(i.Resource, i.PerProduct)}"));
                return recipe.PerHour == Quantity.PerUnit ? $"{inputs} → 1 {one}/h"
                    : Quantity.PerUnit % recipe.PerHour == 0 ? $"{inputs} → 1 {one} per {Quantity.PerUnit / recipe.PerHour} h"
                    : $"{inputs} → 1 {one}, {Amount(recipe.PerHour)}/h";
            }

            if (recipe.HuntTiles > 0)
            {
                return $"{Amount(recipe.PerHour)} {product}/h per skogsruta, högst {Count(recipe.HuntTiles)} · fäller ingen skog";
            }

            if (recipe.ForestTiles > 0)
            {
                return recipe.ForestTiles == 1
                    ? $"{Amount(recipe.PerHour)} {product}/h från en skogsruta"
                    : $"{Amount(recipe.PerHour)} {product}/h per skogsruta, högst {Count(recipe.ForestTiles)}";
            }

            return recipe.IsSlowProcess ? $"{Amount(recipe.GrowsPerHour)} {product}/h dygnet runt" : $"{Amount(recipe.PerHour)} {product}/h";
        }

        /// <summary>Millionths of a unit as a Swedish decimal, such as 1,25.</summary>
        private static string Amount(long millionths)
        {
            string whole = (millionths / Quantity.PerUnit).ToString(System.Globalization.CultureInfo.InvariantCulture);
            long fraction = millionths % Quantity.PerUnit;
            return fraction == 0 ? whole : $"{whole},{fraction.ToString("000000", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0')}";
        }

        private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>A small count in words, as the texts say it; larger counts in digits.</summary>
        public static string Count(int count) => count switch
        {
            2 => "två",
            3 => "tre",
            4 => "fyra",
            5 => "fem",
            6 => "sex",
            7 => "sju",
            8 => "åtta",
            9 => "nio",
            10 => "tio",
            11 => "elva",
            12 => "tolv",
            _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        /// <summary>Which build this is, so the prototype diary can refer to it (TASK-014).</summary>
        public static string Build(string appVersion) => $"Version {appVersion} · regelversion {RulesVersion.Current}";

        public static string Provider(string provider) => provider switch
        {
            Contracts.LoginProviders.Apple => "Apple",
            Contracts.LoginProviders.Google => "Google",
            _ => provider,
        };

        /// <summary>What to tell the player after an account action, or null when there is nothing to say.</summary>
        public static string? Account(Accounts.AccountOutcome outcome) => outcome switch
        {
            Accounts.AccountOutcome.Created => "Kontot är skapat. Välkommen!",
            Accounts.AccountOutcome.Deleted => "Kontot är raderat. Din värld finns kvar på telefonen.",
            Accounts.AccountOutcome.Offline => "Servern går inte att nå just nu. Spelet fungerar som vanligt.",
            Accounts.AccountOutcome.SignedOut => "Du är utloggad. Logga in igen för att fortsätta.",
            Accounts.AccountOutcome.SignInFailed => "Inloggningen gick inte. Försök igen.",
            Accounts.AccountOutcome.ProviderUnavailable => "Den inloggningen finns inte på den här telefonen.",
            Accounts.AccountOutcome.LoginOfAnotherAccount => "Den inloggningen hör redan till ett annat konto.",
            Accounts.AccountOutcome.LastLogin => "Kontot behöver minst en inloggning.",
            Accounts.AccountOutcome.NotThisAccount => "Bekräfta med en inloggning som hör till det här kontot.",
            Accounts.AccountOutcome.ConfirmationUnavailable => "Telefonen kan inte logga in med någon av kontots inloggningar.",
            Accounts.AccountOutcome.Failed => "Det gick inte just nu. Försök igen om en stund.",
            _ => null,
        };

        /// <summary>What to tell the player after a recovery code was tried, or null when the island is being unlocked.</summary>
        public static string? RecoveryCode(App.RecoveryCodeOutcome outcome) => outcome switch
        {
            App.RecoveryCodeOutcome.Unlocked => null,
            App.RecoveryCodeOutcome.NotACode => "Det är inte en hel återställningskod. Kontrollera tecknen.",
            App.RecoveryCodeOutcome.OlderCode => "Koden är från en äldre nyckel. Visa en ny kod på telefonen som har ön.",
            App.RecoveryCodeOutcome.NotThisAccountsCode => "Koden hör inte till det här kontots ö.",
            App.RecoveryCodeOutcome.Offline => "Servern går inte att nå just nu. Försök igen om en stund.",
            _ => "Det gick inte just nu. Försök igen om en stund.",
        };

        /// <summary>How a key transfer stands on the new phone, under the QR code (TASK-023b).</summary>
        public static string KeyTransfer(CloudSaving.KeyTransferOutcome outcome, System.DateTimeOffset? expiresAt) => outcome switch
        {
            CloudSaving.KeyTransferOutcome.Waiting => expiresAt is { } at
                ? $"Väntar på din gamla telefon. Koden gäller till {at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}."
                : "Väntar på din gamla telefon.",
            CloudSaving.KeyTransferOutcome.Received => "Nyckeln kom fram.",
            CloudSaving.KeyTransferOutcome.Expired => "Koden gäller inte längre.",
            CloudSaving.KeyTransferOutcome.Unreadable => "Det som kom gick inte att läsa här. Försök med en ny kod.",
            CloudSaving.KeyTransferOutcome.Rejected => "Nyckeln som kom hör inte till kontots ö. Försök med en ny kod från telefonen som har ön.",
            CloudSaving.KeyTransferOutcome.Offline => expiresAt is { } until
                ? $"Servern går inte att nå just nu. Koden gäller ändå till {until.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}, och försöket fortsätter."
                : "Servern går inte att nå just nu. Försök igen om en stund.",
            CloudSaving.KeyTransferOutcome.SignedOut => "Logga in igen för att hämta nyckeln.",
            _ => "Det gick inte just nu. Försök igen om en stund.",
        };

        /// <summary>What to tell the old phone's player after a scanned code was acted on (TASK-023b).</summary>
        public static string KeyDelivery(CloudSaving.KeyDeliveryOutcome outcome) => outcome switch
        {
            CloudSaving.KeyDeliveryOutcome.Delivered => "Nyckeln är skickad. Den nya telefonen hämtar din ö nu.",
            CloudSaving.KeyDeliveryOutcome.NoKey => "Den här telefonen har ingen nyckel till kontots egenvård.",
            CloudSaving.KeyDeliveryOutcome.BadTicket => "Det är inte en kod från Eirora. Sök koden på den nya telefonen.",
            CloudSaving.KeyDeliveryOutcome.AlreadyDelivered => "En nyckel är redan skickad till den telefonen.",
            CloudSaving.KeyDeliveryOutcome.Expired => "Koden gäller inte längre. Visa en ny på den nya telefonen.",
            CloudSaving.KeyDeliveryOutcome.Offline => "Servern går inte att nå just nu. Försök igen om en stund.",
            CloudSaving.KeyDeliveryOutcome.SignedOut => "Logga in igen för att skicka nyckeln.",
            _ => "Nyckeln kunde inte skickas. Försök igen om en stund.",
        };

        /// <summary>What the old phone's player sees over the camera's picture (TASK-023b).</summary>
        public static string Scanner(ScannerStatus status) => status switch
        {
            ScannerStatus.Starting => "Startar kameran …",
            ScannerStatus.Scanning => "Söker koden …",
            ScannerStatus.Denied => "Eirora får inte använda kameran. Tillåt det i telefonens inställningar, eller skriv in återställningskoden på den nya telefonen.",
            _ => "Den här telefonen har ingen kamera som går att använda. Skriv in återställningskoden på den nya telefonen i stället.",
        };

        /// <summary>How the cloud save stands, for the account sheet, or null when there is no sign-in.</summary>
        public static string? CloudSync(App.CloudSyncStatus status, System.DateTimeOffset? syncedAt) => status switch
        {
            App.CloudSyncStatus.Off => null,
            App.CloudSyncStatus.Working => "Molnsparning: laddar upp …",
            App.CloudSyncStatus.Synced => syncedAt is { } at
                ? $"Molnsparning: din ö är sparad på kontot, senast {at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}"
                : "Molnsparning: din ö är sparad på kontot",
            App.CloudSyncStatus.NoWorld => "Molnsparning: ingen ö att spara ännu",
            App.CloudSyncStatus.ConsentNeeded => "Molnsparning: väntar på ditt svar om egenvården",
            App.CloudSyncStatus.Offline => "Molnsparning: väntar på uppkoppling, din ö sparas när servern nås",
            App.CloudSyncStatus.ChoiceNeeded => "Molnsparning: telefonen och kontot har olika öar",
            App.CloudSyncStatus.KeyNeeded => "Molnsparning: kontots ö väntar på nyckeln från din gamla telefon",
            App.CloudSyncStatus.AppOutdated => "Molnsparning: kontots ö sparades av en nyare version av Eirora. Uppdatera appen.",
            App.CloudSyncStatus.SignedOut => "Molnsparning: logga in igen för att spara din ö på kontot",
            _ => "Molnsparning: gick inte just nu, försöker igen vid nästa ändring",
        };

        public static string Reason(Rejection rejection) => rejection switch
        {
            Rejection.Resting => "Byborna sover. Det här kan du göra i morgon.",
            Rejection.InsufficientResources => "Det finns inte tillräckligt i förrådet än.",
            Rejection.InsufficientForces => "Kraften räcker inte än. Den växer med din egenvård.",
            Rejection.SiteUnavailable => "Det går inte att bygga här.",
            Rejection.AlreadyDone => "Det är redan gjort.",
            Rejection.NothingLeftToSurvey => "Hela ön är kartlagd.",
            Rejection.NothingToRush => "Det finns inget bygge att skynda på.",
            Rejection.NotNextToTheKnown => "Dimman lättar från kanten av det kända. Lyft en ruta närmare först.",
            Rejection.FoundBySurvey => "Något ligger gömt här. Kartlägg ön för att se vad.",
            Rejection.FoundByExpedition => "Här ligger en okänd ö. En expedition kan hitta den.",
            Rejection.NoBuildingThere => "Det behövs en byggnad för det här först.",
            Rejection.GameDayOutOfRange => "Det går att fylla i de senaste sju dygnen.",
            Rejection.AmountOutOfRange => "Värdet verkar inte stämma.",
            Rejection.NoSuchPlace => "Längre än så går den inte att flytta.",
            Rejection.NotQueued => "Det bygget ligger inte i kön längre.",
            Rejection.NoRoomInStore => "Lagret rymmer inte öns varor utan byggnaden. Använd eller skeppa några först.",
            Rejection.LastStorehouse => "Hemöns sista förråd står kvar, eftersom allt betalas ur dess lager.",
            Rejection.NoSuchIsland => "Ön har inget eget lager än.",
            Rejection.NotUnlocked => "Det kan byggas när byn har vuxit.",
            Rejection.NotByHand => "Det här går inte att bygga med handkraft.",
            Rejection.BuiltByHand => "Det byggs med handkraft och står klart på sin dag, hur kön än ordnas.",
            _ => "Det gick inte just nu.",
        };

        /// <summary>
        /// What an island is called: Hemön where the settlement began, Nyön on the prototype's map, "den nya ön" for the
        /// island the first expedition finds on a generated map, whose islands have no names yet, and any other island
        /// by where it lies from the start, such as "ön i väster".
        /// </summary>
        public static string IslandName(World world, string? island)
        {
            if (island == world.HomeIsland)
            {
                return "Hemön";
            }

            if (island == PrototypeHome.Nyon && world.Map.Hash == PrototypeHome.Map.Hash)
            {
                return "Nyön";
            }

            if (world.ExpeditionFinds is Hex finds && world.Map.IslandAt(finds) == island)
            {
                return "den nya ön";
            }

            var anchor = world.Map.Islands.FirstOrDefault(i => i.Id == island)?.Anchor ?? world.Map.Start;
            var way = anchor.Center() - world.Map.Start.Center();
            int point = (int)Math.Round(Math.Atan2(way.z, way.x) / (Math.PI / 4), MidpointRounding.AwayFromZero);
            return "ön i " + s_compass[(point + 8) % 8];
        }

        /// <summary>A building in the definite form, as a sentence begins with it: Sågverket, Fiskeboden.</summary>
        public static string Definite(BuildingKind kind) => kind switch
        {
            BuildingKind.Longhouse => "Långhuset",
            BuildingKind.Cottage => "Stugan",
            BuildingKind.Storehouse => "Förrådet",
            BuildingKind.FishingHut => "Fiskeboden",
            BuildingKind.Field => "Åkern",
            BuildingKind.Woodcutter => "Skogshuggaren",
            BuildingKind.SheepFarm => "Fårgården",
            BuildingKind.Quarry => "Stenbrottet",
            BuildingKind.Sawmill => "Sågverket",
            BuildingKind.Weavery => "Väveriet",
            BuildingKind.Jetty => "Bryggan",
            BuildingKind.Lookout => "Utkikstornet",
            BuildingKind.Farmstead => "Bondgården",
            BuildingKind.HuntingLodge => "Jaktstugan",
            BuildingKind.Tannery => "Garveriet",
            BuildingKind.CraftsmensHouse => "Hantverkargården",
            BuildingKind.Apiary => "Bigården",
            BuildingKind.Brewery => "Bryggeriet",
            BuildingKind.Meadery => "Mjöderiet",
            BuildingKind.TarKiln => "Tjärdalen",
            BuildingKind.Shipyard => "Varvet",
            _ => kind.ToString(),
        };

        /// <summary>
        /// How a building works now, for its sheet beside the ground (TASK-043): how it produces and how much an hour, or
        /// for a building that produces nothing whether it has its villagers; null when it needs none.
        /// </summary>
        public static string? Status(BuildingDiagnosis building, BuildingSpec spec)
        {
            if (building.Product is not Resource product)
            {
                return Staffing(building.Building, spec);
            }

            return building.Building.Paused ? "pausad" : building.Production switch
            {
                Production.Full => $"går för fullt · {Rate(building.PerHour, product)}/h",
                Production.Reduced => $"går långsammare · {Rate(building.PerHour, product)}/h",
                _ => "står still",
            };
        }

        /// <summary>How a building is while the village rests, when nothing is made and nothing is diagnosed.</summary>
        public static string? Resting(Building building, BuildingSpec spec) =>
            !spec.Produces ? Staffing(building, spec) : building.Paused ? "pausad" : "vilar";

        /// <summary>What holds a building back, in a sentence for its sheet (TASK-043, LOG-03).</summary>
        public static string Hindrance(Hindrance hindrance, Resource? product, bool home = true) => hindrance switch
        {
            MissingStaff staff when !home => $"Det saknas {Villagers(staff.Villagers)} här. Öns egna bybor bemannar den, och lediga bybor flyttar hit över sjövägen.",
            MissingStaff staff => $"Det saknas {Villagers(staff.Villagers)} här. Flytta upp den i bemanningen eller pausa en byggnad före den.",
            Paused => "Pausad: byborna bygger i stället. Nivån och lagret står kvar.",
            WaitingForInput { KeptBy: KeptBy.Reservation } input =>
                $"Väntar på {Name(input.Resource)}: {input.Missing} till behövs för en timmes arbete. {input.Kept} är reserverade på ön.",
            WaitingForInput { Kept: 0 } input =>
                $"Väntar på {Name(input.Resource)}: {input.Missing} till behövs för en timmes arbete.",
            WaitingForInput input =>
                $"Väntar på {Name(input.Resource)}: {input.Missing} till behövs för en timmes arbete. Minimilagret på {input.Kept} sparas till byggen.",
            StoreFull { Capacity: 0 } full => $"Ön har inget förråd för {Name(full.Resource)}.",
            StoreFull full => $"Lagret är fullt: {full.Capacity} {Name(full.Resource)}. Inget går förlorat.",
            ForestFelled { InReach: 0 } => "Här finns ingen skog kvar att fälla.",
            ForestFelled forest when forest.Felled == forest.InReach => "Skogen är fälld och växer tillbaka dag och natt.",
            ForestFelled forest => $"{forest.Felled} av {forest.InReach} skogsrutor är fällda. Skogen växer tillbaka dag och natt.",
            NoDepot { DepotStands: true } => "Inget förråd med bärare når hit. Varorna väntar här tills en bybo blir ledig och bär.",
            NoDepot => "Inget förråd når hit över land. Varorna väntar här tills ett förråd byggs.",
            OutboxFull outbox => $"Utlagret är fullt: {outbox.Units} väntar på bärarna, som hämtar dem snart. Inget går förlorat.",
            CarriersBehind behind => Behind(behind, product),
            _ => $"Något håller tillbaka {Rate(hindrance.LostPerHour)} i timmen.",
        };

        /// <summary>
        /// What a processing building's minimum stock keeps, under its name on its sheet (TASK-046): building material, or
        /// the island's reservation when that keeps more.
        /// </summary>
        public static string Reserve(Building building, int reserved, string island) =>
            reserved > building.Reserve ? $"{reserved} reserverade på {island} går före"
            : building.Reserve == 0 ? $"{Definite(building.Kind)} tar allt"
            : "Sparas till byggen";

        /// <summary>What an island's store holds of a good and what is reserved, such as "12 i lagret, 10 reserverade".</summary>
        public static string InStore(long stock, int reserved) =>
            reserved == 0 ? $"{stock} i lagret" : $"{stock} i lagret, {reserved} {(reserved == 1 ? "reserverad" : "reserverade")}";

        /// <summary>What a road to the storehouse gives a building, beside the road's cost on its sheet.</summary>
        public static string RoadGives(CarriersBehind behind, Resource product) =>
            $"{Rate(behind.WithRoadPerHour, product)}/h i stället för {Rate(behind.PerHour)}";

        /// <summary>
        /// A storehouse's or a jetty's carriers in a sentence for its sheet (economy-v2.md, section 7.1): how many, how much
        /// the buildings' goods need of them and how much reaches the store.
        /// </summary>
        public static string Carriers(DepotDiagnosis depot)
        {
            int carriers = depot.Depot.Carriers;
            if (carriers == 0)
            {
                return $"Ingen bybo bär här ännu. {Definite(depot.Depot.Kind)} tar en bärare när en blir ledig.";
            }

            string who = carriers == 1 ? "En bärare" : $"{Capital(Count(carriers))} bärare";
            string need = $"{Rate(depot.NeedPerHour)} bärartimmar i timmen";
            return depot.NeedPerHour == 0 ? $"{who}. Inga byggnader här har något att bära."
                : depot.SharePercent >= 100 ? $"{who}. Byggnaderna behöver {need}, och allt når fram."
                : $"{who}. Byggnaderna behöver {need}, så {depot.SharePercent} % når fram och resten väntar vid byggnaderna.";
        }

        /// <summary>What a depot's advice would bring more to the store, such as "+0,8 fisk/h och +0,3 sten/h".</summary>
        public static string Gains(IEnumerable<Gain> gains)
        {
            var more = gains.Where(g => g.PerHour > 0).Select(g => $"+{Rate(g.PerHour, g.Resource)}/h").ToList();
            return more.Count == 0 ? "ger inget mer i dag" : List(more);
        }

        /// <summary>
        /// The sea route's use now, what it carries out before the island's goods go home, and what keeps goods from
        /// reaching home, for the jetties' sheets.
        /// </summary>
        public static IEnumerable<string> SeaRoute(SeaRouteDiagnosis route)
        {
            yield return route.UsedPercent >= 100 ? "Skeppen går fulla."
                : route.ShippedPerHour == 0 ? "Skeppen har ingen last just nu."
                : $"Skeppen använder {route.UsedPercent} % av sin plats.";
            if (route.MaterialsOwed > 0)
            {
                yield return $"Först tar de ut {route.MaterialsOwed} byggmaterial från Hemön till byggena på ön.";
            }

            if (route.SuppliesPerHour > 0)
            {
                yield return "De tar ut mat från Hemön, så att öns bybor har för två dagar.";
            }

            foreach (var hindrance in route.Hindrances)
            {
                if (hindrance is StoreFull full)
                {
                    yield return $"Lagret hemma är fullt av {Name(full.Resource)}, så den väntar på andra sidan.";
                }
                else if (hindrance is Reserved reserved)
                {
                    yield return $"Skeppen har plats för mer, men {reserved.Units} {Name(reserved.Resource)} är reserverade på ön.";
                }
            }
        }

        /// <summary>An island's largest bottleneck in a sentence, and where a workday eases it (economy-v1.md, section 6).</summary>
        public static string Bottleneck(Bottleneck bottleneck)
        {
            var kind = bottleneck.Building.Building.Kind;
            string building = Definite(kind);
            string held = bottleneck.Hindrance switch
            {
                WaitingForInput input => $"{building} väntar på {Name(input.Resource)}.",
                MissingStaff staff => $"{building} saknar {Villagers(staff.Villagers)}.",
                StoreFull full => $"Lagret är fullt av {Name(full.Resource)}, så {building.ToLowerInvariant()} "
                    + (bottleneck.Building.Production == Production.Stopped ? "står still." : "går långsammare."),
                ForestFelled => $"Skogen kring {building.ToLowerInvariant()} är fälld.",
                CarriersBehind behind when bottleneck.Building.Product is Resource product =>
                    $"Bärarna hinner inte med: {behind.SharePercent} % av {building.ToLowerInvariant()}s {Name(product)} når fram.",
                NoDepot => $"Inget förråd med bärare når {building.ToLowerInvariant()}.",
                _ => $"{building} hålls tillbaka.",
            };

            return bottleneck.Workday is not Building workday ? held
                : bottleneck.Hindrance is WaitingForInput missing ? $"{held} Att arbeta extra på {Definite(workday.Kind).ToLowerInvariant()} ger mer {Name(missing.Resource)}."
                : bottleneck.Hindrance is CarriersBehind ? $"{held} Att arbeta extra på {Definite(workday.Kind).ToLowerInvariant()} låter bärarna bära hälften mer."
                : held;
        }

        /// <summary>Millionths of a unit an hour, to two decimals at most: 0,94.</summary>
        public static string Rate(long millionths)
        {
            long hundredths = (millionths + 5_000) / 10_000;
            string whole = (hundredths / 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
            long fraction = hundredths % 100;
            return fraction == 0 ? whole
                : fraction % 10 == 0 ? $"{whole},{fraction / 10}"
                : $"{whole},{fraction.ToString("00", System.Globalization.CultureInfo.InvariantCulture)}";
        }

        /// <summary>A resource's rate, such as "0,92 plankor" but "1 planka"; the caller adds the hour.</summary>
        private static string Rate(long millionths, Resource resource)
        {
            string rate = Rate(millionths);
            return $"{rate} {(rate == "1" ? Name(resource, 1) : Name(resource))}";
        }

        /// <summary>Why the carriers do not bring all of a building's goods to the store, and what would help.</summary>
        private static string Behind(CarriersBehind behind, Resource? product)
        {
            string steps = behind.Trip.Steps == 1 ? "Ett steg" : $"{Capital(Count(behind.Trip.Steps))} steg";
            string text = $"Bärarna hinner inte: {behind.SharePercent} % når fram. {steps} till förrådet.";
            if (product is not Resource made)
            {
                return text;
            }

            var better = new List<(string What, long PerHour)>();
            if (behind.RoadTiles > 0 && behind.WithRoadPerHour > behind.PerHour)
            {
                better.Add(("en väg", behind.WithRoadPerHour));
            }

            if (behind.WithCarrierPerHour is long carrier && carrier > behind.PerHour)
            {
                better.Add(("en bärare till", carrier));
            }

            if (behind.BesideDepotSite is not null && behind.BesideDepotPerHour > behind.PerHour)
            {
                better.Add(("ett förråd intill", behind.BesideDepotPerHour));
            }

            // The first says what is given and in what; the others only how much.
            var said = better.Select((b, i) => i == 0 ? $"{b.What} ger {Rate(b.PerHour, made)}/h" : $"{b.What} {Rate(b.PerHour)}");
            return better.Count == 0 ? text : $"{text} {Capital(List(said))}.";
        }

        /// <summary>One line in the daily report, or null for events the report leaves out.</summary>
        public static string? Describe(World world, GameEvent gameEvent) => gameEvent switch
        {
            VillagersMovedIn moved when moved.Island != world.HomeIsland =>
                (moved.Count == 1 ? "En ny bybo flyttade in på " : $"{moved.Count} nya bybor flyttade in på ") + IslandName(world, moved.Island) + ".",
            VillagerMoved moved => $"En bybo flyttade från {IslandName(world, moved.From)} till {IslandName(world, moved.To)}.",
            VillagersRose rose when rose.Island != world.HomeIsland => $"{Rose(rose)} på {IslandName(world, rose.Island)}.",
            ProjectCompleted { Kind: ProjectKind.Build, Building: BuildingKind.Jetty } done when world.IslandOf(done.Tile) is var island && island != world.HomeIsland =>
                $"Bryggan på {IslandName(world, island)} är färdig.",
            SettlementFounded founded => $"{Capital(IslandName(world, founded.Island))} har en egen by, dit bybor flyttar över sjövägen.",
            KnarrLaunched launched => $"Varvet på {IslandName(world, launched.Island)} har byggt {(launched.Knarrs == 1 ? "sin första knarr" : "en knarr till")}. "
                + (launched.Island == world.HomeIsland ? "Alla sjövägar bär mer." : "Sjövägen därifrån bär mer."),
            _ => Describe(gameEvent),
        };

        /// <summary>One line in the daily report, or null for events the report leaves out, as it reads on any island.</summary>
        public static string? Describe(GameEvent gameEvent) => gameEvent switch
        {
            DawnBroke dawn => $"Byn vaknade till dag {dawn.GameDay}.",
            ForceCredited { Amount: > 0 } credit => $"+{credit.Amount} {Name(credit.Force)} från din egenvård.",
            VillagersMovedIn moved => moved.Count == 1 ? "En ny bybo flyttade in." : $"{moved.Count} nya bybor flyttade in.",
            VillagersRose rose => $"{Rose(rose)}.",
            StepUnlocked { Step: 1 } => "Byn har vuxit. Bondgård, jaktstuga och garveri kan byggas.",
            StepUnlocked { Step: 2 } => "Bönderna har det gott. Hantverkargård, bigård, bryggeri och mjöderi kan byggas.",
            StepUnlocked { Step: 3 } => "Byn har blivit en hamn. Tjärdal och varv kan byggas.",
            ProjectCompleted { Kind: ProjectKind.Build, Building: BuildingKind built } => $"{Name(built)} är färdig.",
            ProjectCompleted { Kind: ProjectKind.Upgrade, Building: BuildingKind upgraded } => $"{Name(upgraded)} är uppgraderad.",
            ProjectCompleted { Kind: ProjectKind.Road } => "En ny väg är klar.",
            TilesRevealed => "Kartläggningen lyfte dimman över nya rutor.",
            ExpeditionReturned { Island: PrototypeHome.Nyon } => "Expeditionen kom hem. Den hittade Nyön i öster.",
            ExpeditionReturned => "Expeditionen kom hem. Den hittade en ny ö.",
            FogLifted => FogLiftedOver(1),
            BuildingDemolished demolished => $"{Definite(demolished.Building)} revs.",
            _ => null,
        };

        /// <summary>
        /// The daily report: one line per event, except that all the fog the player lifted is one line where the first
        /// lift was, since a day can lift tens of tiles.
        /// </summary>
        public static IEnumerable<string> Report(World world, IEnumerable<GameEvent> events)
        {
            var lines = new List<string>();
            int lifted = 0;
            int fogLine = -1;
            foreach (var gameEvent in events)
            {
                if (gameEvent is FogLifted)
                {
                    if (lifted++ == 0)
                    {
                        fogLine = lines.Count;
                        lines.Add(string.Empty);
                    }

                    continue;
                }

                if (Describe(world, gameEvent) is string line)
                {
                    lines.Add(line);
                }
            }

            if (fogLine >= 0)
            {
                lines[fogLine] = FogLiftedOver(lifted);
            }

            return lines;
        }

        /// <summary>Villagers who rose a tier, as the report begins it: "Två torpare blev bönder", "En bonde blev hantverkare".</summary>
        private static string Rose(VillagersRose rose)
        {
            var below = (PopulationTier)((int)rose.Tier - 1);
            return rose.Count == 1
                ? $"En {One(below)} blev {One(rose.Tier)}"
                : $"{Capital(Count(rose.Count))} {Name(below)} blev {Name(rose.Tier)}";
        }

        /// <summary>
        /// An introduction of a system as it reaches the player (PRG-02; epoch-1.md, section 3): its title and what the
        /// guide says (economy-v2.md, section 5).
        /// </summary>
        public static (string Title, string Text) Introduction(App.Introduction introduction) => introduction switch
        {
            App.Introduction.Farmers => ("Bönder", "Byn har vuxit. Med kläder kan torpare bli bönder. De bor i bondgårdar, och tyg från väveriet eller skinn från garveriet klär dem."),
            App.Introduction.Craftsmen => ("Hantverkare", "Bönderna har det gott. Med dryck kan några bli hantverkare. De bor i hantverkargårdar och bygger dubbelt så fort, och öl från bryggeriet eller mjöd från mjöderiet släcker deras törst."),
            App.Introduction.Seafaring => ("Sjöfart", "Med knarrar bär sjövägen mer. Ett varv vid hamnen bygger dem av plankor och tjära från tjärdalen, och hantverkarna arbetar där. Varje knarr bär tio varor mer per dag på sjövägarna till och från sin ö."),
            _ => (introduction.ToString(), string.Empty),
        };

        private static string FogLiftedOver(int tiles) =>
            tiles == 1 ? "Dimman lättade över en ruta." : $"Dimman lättade över {tiles} rutor.";
    }
}
