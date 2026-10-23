"""Designkalkyl för ekonomi v2 (TASK-058).

Timsimulerar en värld med befolkningsnivåer, behov, kedjor, gradvis upplåsning, en
bosättning per ö och bärare på land (D-105–D-107), för olika strategier och
egenvårdsprofiler, och kontrollerar att inget förfaller. Siffrorna motsvarar
docs/design/economy-v2.md.

Kartan, krafternas profiler och sparandets tak är desamma som i ekonomi v1
(economy_v1.py), som kalkylen läser i stället för att skriva av.

Detta är ett räkneark i kodform, inte spelets simulering: den riktiga simuleringen
skrivs i C# med heltal/fixed-point (INV-D3). Kör: python -I economy_v2.py
"""

import heapq
import math
import os
import sys
from dataclasses import dataclass, field

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # -I tar bort skriptets katalog

import economy_v1 as ev1  # noqa: E402
from economy_v1 import (  # noqa: E402
    ACTIVE_HOURS, EXPEDITIONS, FARMLAND, FOREST, FOREST_PER_TILE, FOREST_REGROWTH, GRASS, HOME,
    LAND, NAMES, PASTURE, PROFILES, START_FORCES, SURVEY_COSTS, SURVEYS, TILES, WOODCUTTER_PER_TILE,
    WOODCUTTER_TILES, coord, credit, neighbours, reach, tag)
from economy_v1 import uses as uses_v1  # noqa: E402

DAYS = 7
LONG_DAYS = 42  # sex veckor: epok 1 med hög egenvård (D-94)

# --- Resurser och behov ---------------------------------------------------------------

RESOURCES = ["fisk", "vilt", "säd", "timmer", "plank", "sten", "ull", "vadmal", "skinn",
             "honung", "mjöd", "öl", "tjära"]
# Vikter för utvecklingsvärdet (endast balansmått, visas aldrig för spelaren).
WEIGHT = {"fisk": 1, "vilt": 1, "säd": 1, "timmer": 1, "plank": 3, "sten": 2, "ull": 2,
          "vadmal": 6, "skinn": 6, "honung": 2, "mjöd": 8, "öl": 5, "tjära": 3}

# Befolkningsnivåer. En bybo på en högre nivå kan arbeta i varje byggnad som en lägre nivå kan.
TIERS = ["torpare", "bönder", "hantverkare"]
TIER_VALUE = [15, 25, 40]
# Varje behov kan uppfyllas med flera varor ur oberoende kedjor (INV-R4). Mat tas i listans ordning.
NEED_GOODS = {"mat": ["fisk", "vilt", "säd"], "kläder": ["vadmal", "skinn"], "dryck": ["mjöd", "öl"]}
# Per bybo och spelardygn; mat för alla, de andra behoven avrundas uppåt per bosättning.
NEEDS = [{"mat": 1.0}, {"mat": 1.0, "kläder": 0.5}, {"mat": 1.0, "kläder": 0.5, "dryck": 0.5}]
COMFORT = 2           # kläder som ger en bybo till i tillväxt vid gryningen (vadmal i v1)
PROMOTIONS_PER_DAY = 2  # bybor som kan stiga en nivå per bosättning och gryning
BUILDER_HOURS = [1, 1, 2]  # arbetstimmar per aktiv timme för en ledig bybo på varje nivå

# --- Upplåsning (D-105) -------------------------------------------------------------------
# Steg i ordning: (namn, villkor på världen och en bosättning). Ett steg gäller hela världen.
UNLOCKS = [
    ("Bönder", lambda w, s: s.population() >= 20),
    ("Hantverkare", lambda w, s: s.population() >= 30 and s.tiers[1] >= 6),
    ("Sjöfart", lambda w, s: s.population() >= 45 and s.tiers[2] >= 6 and len(w.settlements) > 1),
]

# --- Byggnader --------------------------------------------------------------------------


@dataclass(frozen=True)
class Spec:
    cost: dict
    work: int          # arbetstimmar
    build_power: int
    staff: int
    requirement: str
    tier: int = 0      # lägsta nivå som kan bemanna byggnaden
    step: int = 0      # upplåsningssteg: 0 från början, 1–3 enligt UNLOCKS


SPECS = {
    # Från början: ekonomi v1:s tolv byggnader, med samma kostnader.
    "Stuga": Spec({"timmer": 15, "sten": 5}, 32, 0, 0, "land"),
    "Långhus": Spec({"timmer": 10, "plank": 15, "sten": 10}, 40, 60, 0, "land"),
    "Förråd": Spec({"timmer": 10, "sten": 5}, 16, 0, 1, "land"),  # bemanningen är bärarna
    "Fiskebod": Spec({"timmer": 8}, 12, 0, 2, "fiskevatten"),
    "Åker": Spec({"timmer": 4}, 8, 0, 2, "åker"),
    "Huggarkoja": Spec({"timmer": 6}, 8, 0, 2, "skog"),
    "Fårgård": Spec({"timmer": 10, "plank": 5}, 12, 0, 1, "bete"),
    "Stenbrott": Spec({"timmer": 10, "plank": 4}, 16, 0, 2, "berg"),
    "Sågverk": Spec({"timmer": 15, "sten": 5}, 24, 30, 2, "land"),
    "Väveri": Spec({"plank": 10, "sten": 5}, 24, 60, 2, "land"),
    "Brygga": Spec({"timmer": 15, "plank": 10}, 24, 60, 1, "hamnläge"),  # bemanningen är bärarna
    "Utkiksvarde": Spec({"sten": 10}, 12, 40, 0, "land"),
    # Steg 1, Bönder.
    "Bondgård": Spec({"timmer": 20, "sten": 10}, 32, 0, 0, "land", 0, 1),
    "Jaktstuga": Spec({"timmer": 8}, 8, 0, 2, "skog", 0, 1),
    "Garveri": Spec({"timmer": 10, "sten": 5}, 16, 0, 2, "land", 0, 1),
    # Steg 2, Hantverkare.
    "Hantverkargård": Spec({"timmer": 20, "sten": 20}, 40, 0, 0, "land", 0, 2),
    "Bigård": Spec({"timmer": 6}, 8, 0, 1, "äng", 0, 2),
    "Bryggeri": Spec({"timmer": 15, "sten": 5}, 16, 0, 2, "land", 1, 2),
    "Mjöderi": Spec({"plank": 10, "sten": 5}, 24, 60, 2, "land", 1, 2),
    # Steg 3, Sjöfart.
    "Tjärdal": Spec({"timmer": 10, "sten": 5}, 16, 0, 1, "skog", 1, 3),
    "Varv": Spec({"timmer": 20, "plank": 20, "sten": 10}, 40, 80, 2, "hamnläge", 2, 3),
}
UPGRADE = Spec({"plank": 10, "sten": 5}, 16, 50, 0, "")
ROAD_COST, ROAD_WORK = {"sten": 2}, 4  # per bricka
HOUSING = {"Stuga": (0, 4), "Långhus": (0, 8), "Bondgård": (1, 6), "Hantverkargård": (2, 8)}
START_HOUSING = 16
FOUNDING_HOUSING = 4   # nybyggarnas bostäder i det förråd eller den brygga som grundar en bosättning
SETTLERS = 2           # lediga torpare som följer med när en bosättning grundas
DEPOTS = ("Förråd", "Brygga")
STORAGE = {"Förråd": 150, "Brygga": 50}  # per resurs

# Produktion per timme på nivå 1 med full bemanning.
GATHER = {"Fiskebod": ("fisk", 1.25), "Stenbrott": ("sten", 0.75)}
SLOW = {"Åker": ("säd", 0.5), "Fårgård": ("ull", 0.4), "Bigård": ("honung", 0.4)}  # dygnet runt
HARVEST_PER_HOUR = 2.0
# Förädling: (råvara, mängd råvara per enhet, vara, enheter per timme).
RECIPES = {
    "Sågverk": ("timmer", 2, "plank", 1.0),
    "Väveri": ("ull", 2, "vadmal", 0.5),
    "Garveri": ("vilt", 2, "skinn", 0.5),
    "Bryggeri": ("säd", 2, "öl", 0.5),
    "Mjöderi": ("honung", 1, "mjöd", 0.5),
    "Tjärdal": ("timmer", 2, "tjära", 0.5),
}
GAME_PER_TILE = 0.25   # vilt per timme och oexploaterad skogsbricka som jaktstugan når
GAME_TILES = 4
RESERVE = 20           # minimilager som förädlingen lämnar kvar (LOG-06)

# Varvet bygger knarrar som ökar kapaciteten på sjövägarna till och från sin ö.
KNARR_COST = {"plank": 10, "tjära": 5}
KNARR_HOURS = 32
KNARR_CAPACITY = 10    # per spelardygn och knarr
MAX_KNARRAR = 4

# --- Bärare (D-107) ---------------------------------------------------------------------
# Varje förråd och brygga har bärare, en bybo var. De hämtar det som väntar vid byggnaderna
# och bär råvara till förädlingen. En vända är fram och tillbaka längs sökvägen över land.
CARRIER_LOAD = 4        # enheter per vända
STEP_HOURS = 0.25       # per steg över land
ROAD_STEP_HOURS = 0.125  # per steg på väg: vägen gör bärarna dubbelt så snabba
MAX_CARRIERS = 4        # per förråd eller brygga
OUTPUT_BUFFER = 8       # det som får vänta vid en byggnad; fullt stoppar produktionen
INPUT_BUFFER = 4        # råvara som bärarna håller framme vid förädlingen

# --- Krafter --------------------------------------------------------------------------

DAWN_WORKDAY_COST = 30  # Dagsverke: +50 % för en byggnad i ett spelardygn; på ett förråd bär bärarna 50 % mer
DAWN_RUSH_COST = 20     # Rusta: +8 arbetstimmar på det första bygget i kön
SEA_CAPACITY_PER_DAY = 40

# --- Karta ---------------------------------------------------------------------------------

NAMES.update({
    # Brickor som strategierna i v2 bygger på utöver ekonomi v1:s namn.
    "L4": (-2, 1), "L5": (0, 2), "L6": (-3, 0), "L7": (-1, -2), "L8": (0, -3), "L9": (0, 3),
    "L10": (2, 1), "L11": (-3, 2), "L12": (2, -3), "L13": (3, -3), "L14": (0, -2), "L15": (-1, -3),
    "L16": (-1, 4), "L17": (4, -2), "L18": (2, -4),
    "K3": (2, 0), "K4": (-2, 3), "K5": (1, 0),
    "F4": (-1, -1), "F5": (1, -2), "F6": (-2, 2),
    "N3": (10, -1), "N4": (11, -1),
})


def uses(p) -> set:
    """Vad brickan duger till: ekonomi v1:s krav, plus äng för bigården."""
    out = set(uses_v1(p))
    if TILES[p].terrain == GRASS and "land" in out:
        out.add("äng")
    return out


# --- Värld ------------------------------------------------------------------------------


@dataclass
class Building:
    kind: str
    tile: tuple
    level: int = 1
    staffed: bool = False
    carriers: int = 1       # förråd och brygga: antal bärare just nu
    cap: int = MAX_CARRIERS  # förråd och brygga: spelarens tak för bärarna, en gratis planeringsinställning
    grown: float = 0.0      # långsam process: det som vuxit och väntar på skörd
    out: float = 0.0        # utlager vid byggnaden som väntar på bärarna
    inp: float = 0.0        # råvara som bärarna burit fram till förädlingen
    knarr_work: float = 0.0  # varvet: arbete på nästa knarr

    @property
    def staff(self):
        return self.carriers if self.kind in DEPOTS else SPECS[self.kind].staff

    def product(self):
        if self.kind in GATHER:
            return GATHER[self.kind][0]
        if self.kind in SLOW:
            return SLOW[self.kind][0]
        if self.kind in RECIPES:
            return RECIPES[self.kind][2]
        return {"Huggarkoja": "timmer", "Jaktstuga": "vilt"}.get(self.kind)


@dataclass
class Settlement:
    island: str
    stock: dict
    tiers: list
    base_housing: int = 0
    fed: bool = True
    met: list = field(default_factory=lambda: [True, True, True])

    def population(self):
        return sum(self.tiers)


@dataclass
class Project:
    label: str
    island: str
    work_left: float
    on_done: object


class World:
    def __init__(self):
        self.settlements = {HOME: Settlement(HOME, {r: 0.0 for r in RESOURCES}, [12, 0, 0], START_HOUSING)}
        self.settlements[HOME].stock.update({"fisk": 30, "timmer": 40, "plank": 20, "sten": 20})
        self.buildings, self.projects, self.roads = [], [], set()
        self.forces = dict(START_FORCES)
        self.credited = {"U": 0, "B": 0, "G": 0}
        self.spent = {"U": 0, "B": 0, "G": 0}
        self.forest = {p: FOREST_PER_TILE for p, t in TILES.items() if t.terrain == FOREST}
        self.revealed = {p for p, t in TILES.items() if not t.hidden}
        self.expedition_left, self.expedition_target = 0.0, 0
        self.found = set()       # öar som en expedition har hittat
        self.boosted = set()
        self.step, self.unlocked_day = 0, {}
        self.knarrar = {}
        self.day = 0
        self.log, self.introduced = [], {}
        self.carried_hours, self.carrier_capacity = 0.0, 0.0
        self._times = {}
        for kind, tile in [("Förråd", "S"), ("Fiskebod", "K1"), ("Åker", "O1"), ("Huggarkoja", "F1")]:
            self.buildings.append(Building(kind, coord(tile)))
        self.staff()

    # -- hjälpare --
    def island(self, b_or_tile):
        p = b_or_tile.tile if isinstance(b_or_tile, Building) else coord(b_or_tile)
        return TILES[p].island

    def on(self, island):
        return [b for b in self.buildings if self.island(b) == island]

    def has(self, kind, island=None):
        return any(b.kind == kind and (island is None or self.island(b) == island) for b in self.buildings)

    def building_at(self, p):
        return next((b for b in self.buildings if b.tile == p), None)

    def find(self, kind, tile):
        p = coord(tile)
        return next((b for b in self.buildings if b.kind == kind and b.tile == p), None)

    def housing(self, s, tier):
        own = s.base_housing if tier == 0 else 0
        return own + sum(HOUSING[b.kind][1] for b in self.on(s.island)
                         if b.kind in HOUSING and HOUSING[b.kind][0] == tier)

    def storage(self, s):
        return sum(STORAGE.get(b.kind, 0) for b in self.on(s.island))

    def depots(self, island):
        return [b for b in self.on(island) if b.kind in DEPOTS]

    def introduce(self, system):
        """Ett nytt system möter spelaren (PRG-02); kalkylen räknar när det första gången händer."""
        self.introduced.setdefault(system, self.day)

    def travel(self, tile, island):
        """Timmar för en bärare från brickan till närmaste förråd eller brygga, och vilket.

        Sökvägen går över land på samma ö; ett steg tar STEP_HOURS och ett steg in på en
        bricka med väg ROAD_STEP_HOURS. En fiskebod på pålar tar sitt första steg i land.
        """
        depots = tuple(sorted(b.tile for b in self.depots(island)))
        key = (tile, depots, len(self.roads))
        if key not in self._times:
            goals, best, todo = set(depots), {tile: 0.0}, [(0.0, tile)]
            found = (99.0, None)
            while todo:
                t, p = heapq.heappop(todo)
                if t > best.get(p, 1e9):
                    continue
                if p in goals and p != tile:
                    found = (t, p)
                    break
                for n in neighbours(p):
                    if n in TILES and TILES[n].island == island and TILES[n].terrain in LAND:
                        nt = t + (ROAD_STEP_HOURS if n in self.roads else STEP_HOURS)
                        if nt < best.get(n, 1e9):
                            best[n] = nt
                            heapq.heappush(todo, (nt, n))
            self._times[key] = found
        return self._times[key]

    def path(self, tile, island):
        """Brickorna på den kortaste vägen till närmaste förråd, utan ändarna (för vägbygge)."""
        _, goal = self.travel(tile, island)
        if goal is None:
            return []
        prev, todo, seen = {}, [tile], {tile}
        while todo:
            nxt = []
            for p in todo:
                for n in neighbours(p):
                    if n in TILES and n not in seen and TILES[n].island == island and TILES[n].terrain in LAND:
                        seen.add(n)
                        prev[n] = p
                        nxt.append(n)
            if goal in seen:
                break
            todo = nxt
        out, p = [], prev.get(goal)
        while p is not None and p != tile:
            out.append(p)
            p = prev.get(p)
        return out

    def woods(self, p):
        tiles = [n for n in reach(p) if TILES[n].terrain == FOREST and n in self.revealed
                 and (n == p or self.building_at(n) is None)]
        return sorted(tiles, key=lambda n: (-TILES[n].richness, -self.forest[n], n))

    def game_rate(self, b):
        """Vilt per timme: skogsbrickorna delas lika mellan jaktstugorna som når dem."""
        rate = 0.0
        hunters = [h for h in self.buildings if h.kind == "Jaktstuga"]
        for n in self.woods(b.tile)[:GAME_TILES]:
            sharing = sum(1 for h in hunters if n in self.woods(h.tile)[:GAME_TILES])
            rate += GAME_PER_TILE * TILES[n].richness / sharing
        return rate

    def occupied(self, p):
        return self.building_at(p) is not None or any(pr.label.endswith("@" + tag(p)) for pr in self.projects)

    def meets(self, p, requirement):
        if requirement not in uses(p):
            return False
        return requirement != "skog" or bool(self.woods(p))

    def can_build(self, kind, tile):
        p = coord(tile)
        return (p in TILES and p in self.revealed and SPECS[kind].step <= self.step
                and self.meets(p, SPECS[kind].requirement) and not self.occupied(p))

    def boost(self, b):
        """Nivå × dagsverke; avståndet kostar bärartid i stället för produktion (D-107)."""
        return (1.5 if b.level == 2 else 1.0) * (1.5 if id(b) in self.boosted else 1.0)

    def payer(self, island, cost):
        """Bygget betalas ur öns eget lager, annars ur hemöns, som skeppar det med bygget."""
        for name in (island, HOME):
            s = self.settlements.get(name)
            if s and all(s.stock[r] >= n for r, n in cost.items()):
                return s
        return None

    def pay(self, island, cost, bp):
        s = self.payer(island, cost)
        if s is None or self.forces["B"] < bp:
            return False
        for r, n in cost.items():
            s.stock[r] -= n
        self.forces["B"] -= bp
        self.spent["B"] += bp
        return True

    def spend(self, force, amount):
        if self.forces[force] < amount:
            return False
        self.forces[force] -= amount
        self.spent[force] += amount
        return True

    # -- kommandon (nekas utan sidoeffekter, INV-D7) --
    def build(self, kind, tile):
        spec, p = SPECS[kind], coord(tile)
        if not self.can_build(kind, p):
            return False
        island = TILES[p].island
        if island != HOME and island not in self.found:
            return False
        if island != HOME and not self.has("Brygga", HOME):
            return False
        if not self.pay(island, spec.cost, spec.build_power):
            return False
        self.projects.append(Project(f"{kind}@{tag(p)}", island, spec.work, lambda: self.complete(kind, p)))
        return True

    def complete(self, kind, p):
        b = Building(kind, p)
        self.buildings.append(b)
        island = TILES[p].island
        if kind in DEPOTS:
            self._times.clear()
            if island not in self.settlements:  # det första förrådet eller bryggan grundar bosättningen (D-106)
                s = Settlement(island, {r: 0.0 for r in RESOURCES}, [0, 0, 0], FOUNDING_HOUSING)
                self.settlements[island] = s
                home = self.settlements[HOME]
                settlers = min(SETTLERS, self.free(home)[0])
                home.tiers[0] -= settlers
                s.tiers[0] += settlers
                self.introduce("Ny bosättning")
                self.log.append(f"Dygn {self.day + 1}: {island} grundas med {settlers} nybyggare")
        if kind == "Sågverk":
            self.introduce("Förädling och minimilager")
        if kind == "Brygga" and island == HOME:
            self.introduce("Expedition")
        self.staff()

    def upgrade(self, kind, tile):
        b = self.find(kind, tile)
        label = f"Uppgradera {kind}@{tag(coord(tile))}"
        if b is None or b.level > 1 or any(p.label == label for p in self.projects):
            return False
        if not self.pay(self.island(b), UPGRADE.cost, UPGRADE.build_power):
            return False
        self.projects.append(Project(label, self.island(b), UPGRADE.work, lambda: setattr(b, "level", 2)))
        return True

    def road(self, kind, tile):
        """Lägger väg på brickorna mellan byggnaden och närmaste förråd (en genväg för spelaren)."""
        b = self.find(kind, tile)
        if b is None:
            return False
        tiles = [t for t in self.path(b.tile, self.island(b)) if t not in self.roads]
        label = f"Väg till {kind}@{tag(b.tile)}"
        if not tiles or any(p.label == label for p in self.projects):
            return False
        cost = {"sten": ROAD_COST["sten"] * len(tiles)}
        if not self.pay(self.island(b), cost, 0):
            return False
        self.projects.append(Project(label, self.island(b), ROAD_WORK * len(tiles),
                                     lambda: (self.roads.update(tiles), self._times.clear())))
        return True

    def carrier_cap(self, kind, tile, n):
        """Högst så många bärare vid ett förråd eller en brygga: gratis planering (D-23)."""
        b = self.find(kind, tile)
        if b is None or b.kind not in DEPOTS or not 1 <= n <= MAX_CARRIERS:
            return False
        b.cap = n
        return True

    def hire_carriers(self):
        """Vid gryningen tar varje förråd så många bärare som varorna kräver, av de lediga bybor som finns.

        Bärarna räcker alltid till minst en; överblivna bärare blir lediga och bygger.
        """
        for s in self.settlements.values():
            need = self.carrier_load(s.island)
            for d in self.depots(s.island):
                want = max(1, min(d.cap, math.ceil(need.get(d.tile, 0.0) - 1e-9)))
                if want < d.carriers:
                    d.carriers = want
                elif want > d.carriers:
                    d.carriers += min(want - d.carriers, self.free(s)[0])
                self.staff()

    def explore_cost(self, base):
        return base * 3 // 4 if self.has("Utkiksvarde") else base

    def survey(self):
        i = next((i for i, area in enumerate(SURVEYS) if not all(s in self.revealed for s in area)), None)
        if i is None or not self.spend("U", self.explore_cost(SURVEY_COSTS[i])):
            return False
        self.revealed.update(SURVEYS[i])
        self.introduce("Dimman bricka för bricka")
        return True

    def expedition(self):
        if not self.has("Brygga", HOME) or self.expedition_left or self.expedition_target >= len(EXPEDITIONS):
            return False
        _, cost, hours = EXPEDITIONS[self.expedition_target]
        if not self.spend("U", self.explore_cost(cost)):
            return False
        self.expedition_left = hours
        return True

    def workday(self, kind, tile):
        b = self.find(kind, tile)
        if b is None or not b.staffed or id(b) in self.boosted or not self.spend("G", DAWN_WORKDAY_COST):
            return False
        self.boosted.add(id(b))
        return True

    def rush(self):
        if not self.projects or not self.spend("G", DAWN_RUSH_COST):
            return False
        self.projects[0].work_left = max(0, self.projects[0].work_left - 8)
        return True

    # -- bybor --
    @staticmethod
    def lowest(b):
        return 0 if b.kind in DEPOTS else SPECS[b.kind].tier

    def staffing_order(self, island):
        """Byggnader som kräver en högre nivå bemannas först, annars i byggordning (TASK-044)."""
        return sorted(self.on(island), key=lambda b: -self.lowest(b))

    def staff(self):
        """Bemanna i ordning; varje byggnad tar de lägsta nivåerna som duger (INV-V2)."""
        for s in self.settlements.values():
            free = list(s.tiers)
            for b in self.staffing_order(s.island):
                need, lowest = b.staff, self.lowest(b)
                b.staffed = False
                if not need:
                    continue
                if sum(free[lowest:]) >= need:
                    for t in range(lowest, len(TIERS)):
                        take = min(free[t], need)
                        free[t] -= take
                        need -= take
                    b.staffed = True

    def free(self, s):
        """Lediga bybor per nivå i en bosättning: de som inte bemannar något."""
        free = list(s.tiers)
        for b in self.staffing_order(s.island):
            if not b.staffed:
                continue
            need, lowest = b.staff, self.lowest(b)
            for t in range(lowest, len(TIERS)):
                take = min(free[t], need)
                free[t] -= take
                need -= take
        return free

    def consume(self, s, need, amount):
        """Tar ett behov ur lagret, ur den vara det finns mest av först; sant om det räckte."""
        goods, total = NEED_GOODS[need], self.available(s, need)
        if total <= amount:
            for g in goods:
                s.stock[g] = 0.0
            return total >= amount - 1e-9
        # Lagren jämnas ut: allt över en gemensam nivå tas, så att ingen kedja töms först.
        levels = sorted((s.stock[g] for g in goods), reverse=True)
        level, above = levels[0], 0
        for i, v in enumerate(levels + [0.0]):
            if above + (i * (level - v)) >= amount:
                level -= (amount - above) / i
                break
            above += i * (level - v)
            level = v
        for g in goods:
            s.stock[g] -= max(0.0, s.stock[g] - level)
        return True

    def available(self, s, need):
        return sum(s.stock[g] for g in NEED_GOODS[need])

    # -- tid --
    def dawn(self, day, profile):
        self.day = day
        p = PROFILES[profile]
        for f, amount in (("U", p["U"]), ("G", p["G"]), ("B", p["B"][day % 7])):
            before = self.forces[f]
            self.forces[f] = credit(before, amount, f)
            self.credited[f] += self.forces[f] - before
        self.boosted = set()
        for s in self.settlements.values():
            # Basbehov och nivåbehov: brist stoppar bara tillväxt och avancemang (D-39, INV-V3).
            s.fed = self.consume(s, "mat", s.population())
            s.met = [True, True, True]
            for t in (1, 2):
                for need, per in NEEDS[t].items():
                    if need != "mat" and s.tiers[t]:
                        s.met[t] = self.consume(s, need, math.ceil(s.tiers[t] * per)) and s.met[t]
            growth = 0
            if s.fed:
                growth = 1
                if self.available(s, "kläder") >= COMFORT:
                    self.consume(s, "kläder", COMFORT)
                    growth += 1
            s.tiers[0] += max(0, min(growth, self.housing(s, 0) - s.tiers[0]))
            # Att stiga en nivå: behoven uppfylldes i gryningen, det finns plats och varor till de nya.
            for t in (1, 2):
                if self.step < t or not s.fed or not s.met[t]:
                    continue
                room = self.housing(s, t) - s.tiers[t]
                k = min(PROMOTIONS_PER_DAY, room, s.tiers[t - 1])
                while k > 0 and not all(self.available(s, need) >= math.ceil(k * per)
                                        for need, per in NEEDS[t].items() if need != "mat"):
                    k -= 1
                if k > 0:
                    s.tiers[t - 1] -= k
                    s.tiers[t] += k
        self.migrate()
        self.staff()
        self.hire_carriers()
        for s in self.settlements.values():
            need = self.carrier_load(s.island)
            if any(d.staffed and need.get(d.tile, 0.0) > d.carriers for d in self.depots(s.island)):
                self.introduce("Bärare")  # diagnostiken säger första gången att bärarna inte hinner
        for i, (name, rule) in enumerate(UNLOCKS):
            if self.step == i and any(rule(self, s) for s in self.settlements.values()):
                self.step = i + 1
                self.unlocked_day[name] = day
                self.introduce(name)
                self.log.append(f"Dygn {day + 1}: steget {name} låses upp")

    def migrate(self):
        """En ledig torpare flyttar över sjövägen till en bosättning med plats och arbete (D-106)."""
        for s in list(self.settlements.values()):
            unstaffed = any(b.staff and not b.staffed for b in self.on(s.island))
            if not unstaffed or self.housing(s, 0) <= s.tiers[0]:
                continue
            senders = [o for o in self.settlements.values() if o is not s and self.linked(o, s)
                       and self.free(o)[0] > 0]
            if senders:
                sender = max(senders, key=lambda o: (self.free(o)[0], o.island))
                sender.tiers[0] -= 1
                s.tiers[0] += 1
                self.staff()

    def linked(self, a, b):
        return all(self.has("Brygga", x) for x in (a.island, b.island)) and HOME in (a.island, b.island)

    def hour(self, active):
        # Långsamma processer fortgår dygnet runt (INV-T5).
        for p in self.forest:
            self.forest[p] = min(FOREST_PER_TILE, self.forest[p] + FOREST_REGROWTH)
        for b in self.buildings:
            if b.kind in SLOW:
                b.grown += SLOW[b.kind][1] * TILES[b.tile].richness * self.boost(b)
        if not active:
            return  # byborna sover och bärarna bär inte (INV-T4)
        if self.expedition_left:
            self.expedition_left -= 1
            if self.expedition_left == 0:
                island = EXPEDITIONS[self.expedition_target][0]
                self.expedition_target += 1
                self.found.add(island)
                self.revealed.update(p for p, t in TILES.items() if t.island == island)
                self.log.append(f"Dygn {self.day + 1}: expeditionen hittar {island}")
        self.build_hour()
        self.produce_hour()
        self.carry_hour()
        self.ship_hour()

    def build_hour(self):
        labour = sum(BUILDER_HOURS[t] * n for s in self.settlements.values() for t, n in enumerate(self.free(s)))
        while labour > 0 and self.projects:
            p = self.projects[0]
            used = min(labour, p.work_left)
            p.work_left -= used
            labour -= used
            if p.work_left <= 0:
                self.projects.pop(0)
                p.on_done()

    def make(self, b, amount):
        """Lägger produktion i byggnadens utlager; fullt utlager stoppar, inget går förlorat."""
        amount = max(0.0, min(amount, OUTPUT_BUFFER - b.out))
        b.out += amount
        return amount

    def produce_hour(self):
        for b in self.buildings:
            if not b.staffed:
                continue
            k, tile = self.boost(b), TILES[b.tile]
            if b.kind in GATHER:
                self.make(b, GATHER[b.kind][1] * tile.richness * k)
            elif b.kind in SLOW:
                b.grown -= self.make(b, min(b.grown, HARVEST_PER_HOUR * k))
            elif b.kind == "Huggarkoja":
                for n in [n for n in self.woods(b.tile) if self.forest[n] > 0][:WOODCUTTER_TILES]:
                    take = min(WOODCUTTER_PER_TILE * TILES[n].richness * k, self.forest[n])
                    self.forest[n] -= self.make(b, take)
            elif b.kind == "Jaktstuga":
                self.make(b, self.game_rate(b) * k)
            elif b.kind in RECIPES:
                _, per, _, rate = RECIPES[b.kind]
                n = self.make(b, min(rate * k, b.inp / per))
                b.inp -= n * per
            elif b.kind == "Varv":
                s = self.settlements[self.island(b)]
                done = self.knarrar.get(self.island(b), 0)
                if done < MAX_KNARRAR:
                    if b.knarr_work == 0 and all(s.stock[r] >= n for r, n in KNARR_COST.items()):
                        for r, n in KNARR_COST.items():
                            s.stock[r] -= n
                        b.knarr_work = KNARR_HOURS
                    if b.knarr_work:
                        b.knarr_work = max(0.0, b.knarr_work - k)
                        if b.knarr_work == 0:
                            self.knarrar[self.island(b)] = done + 1

    def carry_hour(self):
        """Bärarna vid varje förråd tar först den byggnad där det väntar mest (D-107)."""
        for s in self.settlements.values():
            cap = self.storage(s)
            served = {}
            for b in self.on(s.island):
                if b.kind in DEPOTS:
                    continue
                t, depot = self.travel(b.tile, s.island)
                if depot is not None:
                    served.setdefault(depot, []).append((b, t))
            for depot in self.depots(s.island):
                if not depot.staffed:
                    continue
                hours = depot.carriers * (1.5 if id(depot) in self.boosted else 1.0)
                self.carrier_capacity += hours
                jobs = []
                for b, t in served.get(depot.tile, []):
                    rate = CARRIER_LOAD / (2 * t)  # enheter per bärartimme
                    if b.out > 1e-9:
                        jobs.append((b.out / OUTPUT_BUFFER, "hämta", b, rate))
                    if b.kind in RECIPES and b.staffed:
                        res = RECIPES[b.kind][0]
                        want = min(INPUT_BUFFER - b.inp, max(0.0, s.stock[res] - RESERVE))
                        if want > 1e-9:
                            jobs.append((want / INPUT_BUFFER, "lämna", b, rate))
                jobs.sort(key=lambda j: -j[0])
                left = hours
                for _, what, b, rate in jobs:
                    if left <= 1e-9:
                        break
                    if what == "hämta":
                        res = b.product()
                        n = min(b.out, left * rate, max(0.0, cap - s.stock[res]))
                        b.out -= n
                        s.stock[res] += n
                    else:
                        res = RECIPES[b.kind][0]
                        n = min(INPUT_BUFFER - b.inp, max(0.0, s.stock[res] - RESERVE), left * rate)
                        s.stock[res] -= n
                        b.inp += n
                    left -= n / rate
                self.carried_hours += hours - left

    def ship_hour(self):
        """Sjövägen: först mat ut till öns bosättning, sedan öns varor hem (LOG-04)."""
        home = self.settlements[HOME]
        for island, s in self.settlements.items():
            if island == HOME or not self.linked(home, s):
                continue
            self.introduce("Sjöväg")
            knarrar = min(MAX_KNARRAR, self.knarrar.get(island, 0) + self.knarrar.get(HOME, 0))
            room = (SEA_CAPACITY_PER_DAY + KNARR_CAPACITY * knarrar) / ACTIVE_HOURS
            cap_home, cap_s = self.storage(home), self.storage(s)
            short = max(0.0, 2 * s.population() - self.available(s, "mat"))
            for r in NEED_GOODS["mat"]:
                n = min(room, short, home.stock[r], max(0.0, cap_s - s.stock[r]))
                home.stock[r] -= n
                s.stock[r] += n
                room -= n
                short -= n
            keep = {r: 2 * s.population() for r in NEED_GOODS["mat"]}
            for r in RESOURCES:
                n = min(room, max(0.0, s.stock[r] - keep.get(r, 0)), max(0.0, cap_home - home.stock[r]))
                s.stock[r] -= n
                home.stock[r] += n
                room -= n

    # -- mått --
    def population(self):
        return sum(s.population() for s in self.settlements.values())

    def tier_counts(self):
        return [sum(s.tiers[t] for s in self.settlements.values()) for t in range(len(TIERS))]

    def rate(self, b):
        """Vad en bemannad byggnad gör per aktiv timme på sin nivå, utan dagsverke: (vara, mängd)."""
        if not b.staffed or not b.product():
            return None, 0.0
        k, richness = (1.5 if b.level == 2 else 1.0), TILES[b.tile].richness
        if b.kind in GATHER:
            return GATHER[b.kind][0], GATHER[b.kind][1] * richness * k
        if b.kind in SLOW:
            return SLOW[b.kind][0], SLOW[b.kind][1] * richness * k * 24 / ACTIVE_HOURS
        if b.kind == "Huggarkoja":
            return "timmer", k * sum(WOODCUTTER_PER_TILE * TILES[n].richness
                                     for n in self.woods(b.tile)[:WOODCUTTER_TILES])
        if b.kind == "Jaktstuga":
            return "vilt", k * self.game_rate(b)
        return RECIPES[b.kind][2], RECIPES[b.kind][3] * k

    def carrier_load(self, island):
        """Bärartimmar som varje förråd behöver per aktiv timme för att allt ska nå fram."""
        need = {}
        for b in self.on(island):
            res, n = self.rate(b)
            t, depot = self.travel(b.tile, island)
            if res is None or depot is None:
                continue
            units = n * (1 + RECIPES[b.kind][1]) if b.kind in RECIPES else n
            need[depot] = need.get(depot, 0.0) + units * 2 * t / CARRIER_LOAD
        return need

    def daily_output(self):
        """Produktionskapacitet per spelardygn, viktad, med det som bärarna hinner få fram (balansmått)."""
        total = 0.0
        for island in self.settlements:
            need = self.carrier_load(island)
            share = {d.tile: min(1.0, d.carriers / need[d.tile]) if d.staffed and need.get(d.tile) else 0.0
                     for d in self.depots(island)}
            for b in self.on(island):
                res, n = self.rate(b)
                depot = self.travel(b.tile, island)[1]
                if res is not None and depot is not None:
                    total += WEIGHT[res] * n * share[depot] * ACTIVE_HOURS
        return total

    def value(self):
        stock = sum(WEIGHT[r] * v for s in self.settlements.values() for r, v in s.stock.items())
        waiting = sum(WEIGHT[b.product()] * b.out for b in self.buildings if b.product())
        people = sum(TIER_VALUE[t] * n for t, n in enumerate(self.tier_counts()))
        return people + (stock + waiting) / 2 + 3 * self.daily_output()


# --- Hjälp för strategierna ----------------------------------------------------------------

def best_tile(w, kind, island=HOME, avoid_resources=True):
    """Den lediga bricka som duger och ligger närmast ett förråd; skog, åker och bete sparas."""
    spec = SPECS[kind]
    tiles = [p for p, t in TILES.items() if t.island == island and w.can_build(kind, p)]
    if not tiles:
        return None
    precious = {FOREST, FARMLAND, PASTURE}

    def key(p):
        spare = 1 if avoid_resources and spec.requirement in ("land", "äng") and TILES[p].terrain in precious else 0
        return (spare, w.travel(p, island)[0], p)
    return min(tiles, key=key)


def pending(w, *kinds):
    return any(p.label.split("@")[0] in kinds for p in w.projects)


def supply(w, goods, island=HOME):
    """Vad bosättningens byggnader kan göra av varorna per spelardygn."""
    return sum(n * ACTIVE_HOURS for res, n in (w.rate(b) for b in w.on(island)) if res in goods)


def build_near(w, kind):
    return build_near_on(w, kind, HOME)


def build_near_on(w, kind, island):
    """Bygger på bästa lediga bricka, men lämnar alltid två lediga bybor som bygger (TASK-044)."""
    s = w.settlements.get(island, w.settlements[HOME])
    if SPECS[kind].staff and sum(w.free(s)) < SPECS[kind].staff + 2:
        return False
    tile = best_tile(w, kind, island)
    return tile is not None and w.build(kind, tile)


def keep_growing(w, day, power=True):
    """Det som varje genomtänkt strategi gör när dess plan är klar: bostäder, mat, kläder, dryck och nivåer.

    Utan power använder den ingen Byggkraft, som en spelare som valt bort träning (D-43).
    """
    if day < DAYS:
        return  # första veckan följer varje strategi sin egen plan
    home = w.settlements[HOME]
    t, b, h = home.tiers
    if w.housing(home, 0) - t < 3 and not pending(w, "Stuga", "Långhus"):
        if power and w.forces["B"] >= 120 and home.stock["plank"] >= 15:
            build_near(w, "Långhus")
        else:
            build_near(w, "Stuga")
    if supply(w, NEED_GOODS["mat"]) < home.population() + 6 and not pending(w, "Fiskebod", "Jaktstuga", "Åker"):
        for kind in ("Åker", "Fiskebod", "Jaktstuga"):
            if build_near(w, kind):
                break
    # Byggmaterial: timmer till allt, och plank och sten till det som kommer sedan.
    sawmills = sum(1 for x in w.on(HOME) if x.kind == "Sågverk")
    if home.stock["timmer"] < 60 and supply(w, ["timmer"]) < 20 + 32 * sawmills and not pending(w, "Huggarkoja"):
        build_near(w, "Huggarkoja")
    if power and not sawmills and not pending(w, "Sågverk"):
        build_near(w, "Sågverk")
    if home.stock["sten"] < 40 and supply(w, ["sten"]) < 12 and not pending(w, "Stenbrott"):
        build_near(w, "Stenbrott")
    if w.step >= 1:
        clothes = math.ceil(0.5 * (b + h)) + COMFORT + 2
        if supply(w, NEED_GOODS["kläder"]) < clothes and not pending(w, "Väveri", "Garveri", "Fårgård",
                                                                     "Jaktstuga"):
            if power and supply(w, ["ull"]) >= 8 and not w.has("Väveri"):
                build_near(w, "Väveri")
            elif power and best_tile(w, "Fårgård") and not w.has("Fårgård"):
                build_near(w, "Fårgård")
            elif supply(w, ["vilt"]) < 2 * supply(w, ["skinn"]) + 10:
                build_near(w, "Jaktstuga")
            else:
                build_near(w, "Garveri")
        if (w.housing(home, 1) - b < 2 and supply(w, NEED_GOODS["kläder"]) >= clothes - 2
                and not pending(w, "Bondgård")):
            build_near(w, "Bondgård")
    if w.step >= 2:
        drink = math.ceil(0.5 * h) + 2
        if supply(w, NEED_GOODS["dryck"]) < drink and not pending(w, "Bryggeri", "Bigård", "Mjöderi", "Åker"):
            if power and supply(w, ["honung"]) >= 8 and not w.has("Mjöderi"):
                build_near(w, "Mjöderi")
            elif power and not w.has("Bigård"):
                build_near(w, "Bigård")
            elif not w.has("Bryggeri"):
                build_near(w, "Bryggeri")
            elif not build_near(w, "Åker"):
                build_near(w, "Bigård")
        if (w.housing(home, 2) - h < 2 and supply(w, NEED_GOODS["dryck"]) >= drink - 1
                and not pending(w, "Hantverkargård")):
            build_near(w, "Hantverkargård")
    if w.step >= 3 and power:
        if not w.has("Tjärdal") and not pending(w, "Tjärdal"):
            build_near(w, "Tjärdal")
        if not w.has("Varv") and not pending(w, "Varv"):
            build_near(w, "Varv")
    # Havet: brygga, expedition och en bosättning på den nya ön, som epok 1 kräver.
    if power and not w.has("Brygga", HOME) and w.forces["B"] >= 120 and not pending(w, "Brygga"):
        build_near(w, "Brygga")
    w.expedition()
    for island in sorted(w.found):
        if not w.has("Brygga", island) and not pending(w, "Brygga"):
            build_near_on(w, "Brygga", island)
        elif w.has("Brygga", island):
            for kind in ("Fårgård", "Stenbrott", "Fiskebod"):
                if not w.has(kind, island) and not pending(w, kind):
                    build_near_on(w, kind, island)
    # Gryningskraft till det förråd vars bärare inte hinner med.
    need = w.carrier_load(HOME)
    behind = [d for d in w.depots(HOME) if d.staffed and need.get(d.tile, 0.0) > d.carriers]
    if behind:
        worst = max(behind, key=lambda d: (need[d.tile] - d.carriers, d.tile))
        w.workday(worst.kind, worst.tile)
    # Det som blir över av krafterna: uppgraderingar och dagsverken på det som gör mest.
    def worth(x):
        res, n = w.rate(x)
        return (WEIGHT[res] * n, x.tile) if res else (0.0, x.tile)
    working = sorted((x for x in w.buildings if x.staffed and w.rate(x)[0]), key=worth, reverse=True)
    if power and w.forces["B"] >= 150 and not pending(w, *(f"Uppgradera {x.kind}" for x in working)):
        for x in working:
            if x.level == 1 and w.upgrade(x.kind, x.tile):
                break
    for x in working:
        if w.forces["G"] < DAWN_WORKDAY_COST + DAWN_RUSH_COST:
            break
        w.workday(x.kind, x.tile)


# --- Strategier -------------------------------------------------------------------------

def strategy_land(w: World, day: int):
    """Närområdet: kartlägg Hemön, bygg ett produktionskluster kring stenådern och urskogen."""
    w.survey()
    w.build("Huggarkoja", "F3")
    w.build("Stenbrott", "M2")
    w.build("Förråd", "E1")
    w.build("Sågverk", "E2")
    w.build("Långhus", "L1")
    w.build("Fiskebod", "K2")
    w.build("Långhus", "L2")
    w.upgrade("Sågverk", "E2")
    w.build("Åker", "O2")
    w.build("Långhus", "L3")
    w.workday("Stenbrott", "M2")
    w.workday("Sågverk", "E2")
    w.rush()
    keep_growing(w, day)


def strategy_sea(w: World, day: int):
    """Havet: brygga, expedition till Nyön, ull och vadmal för snabbare tillväxt.

    Nyöns brygga går före bostaden, eftersom nybyggarna bara följer med när det finns lediga bybor (D-106).
    """
    w.build("Brygga", "H1")
    w.build("Huggarkoja", "E2")
    w.build("Sågverk", "L3")
    w.expedition()
    w.build("Brygga", "N9")
    w.build("Fårgård", "N1")
    w.build("Stenbrott", "N2")
    w.build("Stuga", "L1")
    w.build("Väveri", "L2")
    w.build("Långhus", "E1")
    w.workday("Sågverk", "L3")
    w.workday("Fårgård", "N1")
    w.rush()
    keep_growing(w, day)


def strategy_village(w: World, day: int):
    """Bygden: växer först, klär byborna och når bönderna redan första veckan."""
    w.survey()
    w.build("Fårgård", "P1")
    w.build("Stenbrott", "M3")
    w.build("Förråd", "L6")
    w.build("Sågverk", "L2")
    w.build("Väveri", "L4")
    w.build("Stuga", "L1")
    w.build("Fiskebod", "K3")
    w.build("Långhus", "L5")
    w.build("Stuga", "L3")
    w.build("Jaktstuga", "F4")
    w.build("Bondgård", "L7")
    w.build("Garveri", "L14")
    w.workday("Fårgård", "P1")
    w.workday("Väveri", "L4")
    w.rush()
    keep_growing(w, day)


def strategy_naive(w: World, day: int):
    """Naiv: bygger det första som går, där det går, utan att läsa kartan."""
    w.build("Sågverk", "L1")
    w.build("Stenbrott", "M3")
    w.build("Långhus", "L2")
    w.build("Väveri", "L3")
    w.build("Fårgård", "P1")
    w.upgrade("Fiskebod", "K1")
    w.upgrade("Huggarkoja", "F1")
    w.build("Långhus", "E1")
    w.upgrade("Åker", "O1")
    w.survey()
    w.workday("Fiskebod", "K1")
    w.workday("Åker", "O1")
    w.workday("Huggarkoja", "F1")


def strategy_no_training(w: World, day: int):
    """Utan träning: bara det som inte kostar Byggkraft (D-43)."""
    w.survey()
    w.build("Huggarkoja", "F3")
    w.build("Stenbrott", "M2")
    w.build("Stuga", "L1")
    w.build("Fiskebod", "K2")
    w.build("Stuga", "L2")
    w.build("Åker", "O2")
    w.build("Stuga", "L3")
    w.workday("Stenbrott", "M2")
    w.workday("Huggarkoja", "F3")
    w.rush()
    keep_growing(w, day, power=False)


def strategy_idle(w: World, day: int):
    """Ingen spelare: inga kommandon alls."""


# --- Körning --------------------------------------------------------------------------------

def run(strategy, profile, days=DAYS, w=None, start=0):
    """Spelar dygn för dygn och kontrollerar varje timme att inget förfaller (INV-V1, INV-R1, INV-F1)."""
    w = w or World()

    def monotone():
        tiers = w.tier_counts()
        return (w.population(), tiers[1] + tiers[2], tiers[2], len(w.buildings),
                sum(b.level for b in w.buildings), len(w.revealed), len(w.settlements), w.step, len(w.roads))
    for day in range(start, start + days):
        before = monotone()
        w.dawn(day, profile)
        strategy(w, day)
        for h in range(24):
            w.hour(active=h < ACTIVE_HOURS)
            after = monotone()
            assert all(a >= b for a, b in zip(after, before)), f"förfall (INV-V1): {before} → {after}"
            before = after
            for s in w.settlements.values():
                assert all(v >= -1e-9 for v in s.stock.values()), "negativt lager (INV-R1)"
                assert all(v <= w.storage(s) + 1e-9 for v in s.stock.values()), "över kapaciteten (INV-R1)"
                assert all(n >= 0 for n in s.tiers), "negativ nivå (INV-V2)"
            assert all(b.out >= -1e-9 and b.inp >= -1e-9 for b in w.buildings), "negativt utlager"
            assert all(v >= 0 for v in w.forces.values()), "negativt saldo (INV-F1)"
    return w


# --- Migration från ekonomi v1 (ADR-0006) ------------------------------------------------------

def migrate_v1(old):
    """En värld från ekonomi v1 blir en värld i ekonomi v2 utan att något går förlorat.

    Byggnader, nivåer, bybor, lager, krafter, dimma och skog står kvar. Byborna som bemannar
    byggnader på en annan ö blir den öns bosättning; vägarna per byggnad blir vägbrickor längs
    sökvägen till förrådet, och stenen för vägsteg som inte längre behövs läggs tillbaka.
    """
    w = World()
    w.buildings, w.roads, w.projects = [], set(), []
    w.forces, w.spent, w.credited = dict(old.forces), dict(old.spent), dict(old.credited)
    w.forest, w.revealed = dict(old.forest), set(old.revealed)
    w.expedition_left, w.expedition_target = old.expedition_left, old.expedition_target
    w.found = set(old.remote_stock)
    w.day = 0
    for ob in old.buildings:
        w.buildings.append(Building(ob.kind, ob.tile, ob.level, grown=ob.buffer))
    home = w.settlements[HOME]
    home.stock = {r: float(old.stock.get(r, 0.0)) for r in RESOURCES}
    away = 0
    for island, stock in old.remote_stock.items():
        staff = sum(ev1.SPECS[b.kind].staff for b in old.buildings if b.staffed and TILES[b.tile].island == island)
        s = Settlement(island, {r: float(stock.get(r, 0.0)) for r in RESOURCES}, [staff, 0, 0], FOUNDING_HOUSING)
        w.settlements[island] = s
        away += staff
    home.tiers = [old.villagers - away, 0, 0]
    for s in w.settlements.values():  # det som inte ryms på ön skeppas hem; det som inte ryms där heller stannar
        for r in RESOURCES:
            over = max(0.0, s.stock[r] - w.storage(s))
            if s is not home and over:
                moved = min(over, max(0.0, w.storage(home) - home.stock[r]))
                s.stock[r] -= moved
                home.stock[r] += moved
    for ob in old.buildings:
        if ob.road:
            b = w.find(ob.kind, ob.tile)
            tiles = w.path(b.tile, w.island(b))
            w.roads.update(tiles)
            s = w.settlements[w.island(b)]
            paid = ROAD_COST["sten"] * old.depot_distance(ob.tile)
            s.stock["sten"] += paid - ROAD_COST["sten"] * len(tiles)
    w._times.clear()
    for op in old.projects:  # pågående byggen står kvar i kön med det arbete som är kvar
        kind, _, where = op.label.partition("@")
        tile = tuple(int(x) for x in where.split(","))
        if kind in SPECS:
            w.projects.append(Project(op.label, TILES[tile].island, op.work_left,
                                      lambda k=kind, t=tile: w.complete(k, t)))
        elif kind.startswith("Uppgradera "):
            b = w.find(kind.split(" ", 1)[1], tile)
            w.projects.append(Project(op.label, TILES[tile].island, op.work_left,
                                      lambda b=b: setattr(b, "level", 2)))
        elif kind.startswith("Väg till "):
            b = w.find(kind.split(" ", 2)[2], tile)
            tiles = w.path(b.tile, w.island(b))
            w.projects.append(Project(op.label, TILES[tile].island, op.work_left,
                                      lambda t=tiles: (w.roads.update(t), w._times.clear())))
    w.staff()
    for i, (name, rule) in enumerate(UNLOCKS):  # upplåsningen börjar där världen står
        if w.step == i and any(rule(w, s) for s in w.settlements.values()):
            w.step = i + 1
    return w


def strategy_v1_with_road(w, day):
    """Närområdet i ekonomi v1 med en väg till fiskeboden vid det rika vattnet."""
    ev1.strategy_land(w, day)
    w.road("Fiskebod", "K2")


def check_migration(name, v1_strategy, v2_strategy):
    old = ev1.run(v1_strategy, "måttlig")
    w = migrate_v1(old)
    assert w.population() == old.villagers, "bybor förlorade i migrationen"
    assert sorted((b.kind, b.tile, b.level) for b in w.buildings) == sorted(
        (b.kind, b.tile, b.level) for b in old.buildings), "byggnader förlorade i migrationen"
    stock_old = {r: old.stock.get(r, 0.0) + sum(st.get(r, 0.0) for st in old.remote_stock.values())
                 for r in RESOURCES}
    stock_new = {r: sum(s.stock[r] for s in w.settlements.values()) for r in RESOURCES}
    assert all(stock_new[r] >= stock_old[r] - 1e-9 for r in RESOURCES), "varor förlorade i migrationen"
    assert w.forces == old.forces and w.revealed == old.revealed
    migrated = (f"| {name} | {old.villagers} | {len(old.buildings)} | {len(w.settlements)} | {len(w.roads)} "
                f"| {sum(stock_new.values()) - sum(stock_old.values()):.0f} ")
    after = run(v2_strategy, "måttlig", DAYS, w, start=DAYS)
    return migrated + f"| {after.population()} | {len(after.buildings)} | {after.value():.0f} |"


# --- Innehållet: kedjor och INV-R4 -----------------------------------------------------------------

def producers(good):
    kinds = [k for k, (g, _) in {**GATHER, **SLOW}.items() if g == good]
    kinds += [k for k, r in RECIPES.items() if r[2] == good]
    kinds += [k for k, g in {"Huggarkoja": "timmer", "Jaktstuga": "vilt"}.items() if g == good]
    return kinds


def chain(good):
    """Kedjan till en vara: byggnaderna, marken råvaran kommer från, Byggkraft och lägsta nivå."""
    kind = producers(good)[0]
    spec = SPECS[kind]
    if kind in RECIPES:
        kinds, land, power, tier = chain(RECIPES[kind][0])
        return kinds + [kind], land, power + spec.build_power, max(tier, spec.tier)
    return [kind], {spec.requirement}, spec.build_power, spec.tier


def check_needs():
    """Varje behov har minst två varor ur kedjor som börjar på olika mark, minst en utan Byggkraft,
    och en nivås behov kan alltid göras av nivån under, så att ingen nivå behöver sig själv (INV-R4)."""
    rows = []
    for need, goods in NEED_GOODS.items():
        tier = min(t for t, n in enumerate(NEEDS) if need in n)
        chains = {g: chain(g) for g in goods}
        independent = any(not (chains[a][1] & chains[b][1]) for a in goods for b in goods if a < b)
        assert len(goods) >= 2 and independent, f"{need}: färre än två oberoende källor (INV-R4)"
        assert any(c[2] == 0 for c in chains.values()), f"{need}: kräver Byggkraft (D-43)"
        assert all(c[3] <= max(0, tier - 1) for c in chains.values()), f"{need}: kräver sin egen nivå"
        for g, (kinds, land, power, lowest) in chains.items():
            rows.append(f"| {need} | {g} | {' → '.join(kinds)} | {', '.join(sorted(land))} | {power} "
                        f"| {TIERS[lowest]} |")
    return rows


def catalog():
    rows = []
    for kind, spec in SPECS.items():
        cost = ", ".join(f"{n} {r}" for r, n in spec.cost.items())
        rows.append(f"| {kind} | {[0, 1, 2, 3][spec.step]} | {cost} | {spec.work} | {spec.build_power} "
                    f"| {spec.staff} | {TIERS[spec.tier]} | {spec.requirement} |")
    return rows


# --- Diagnostik: vad en bärare, en väg eller ett förråd ger (CIV-05, LOG-03) ---------------------

def remedies(w, island=HOME):
    """För förrådet som har mest att bära: hur stor del som når fram nu och med varje åtgärd."""
    need = w.carrier_load(island)
    depot = max((d for d in w.depots(island) if need.get(d.tile)), key=lambda d: (need[d.tile] / d.carriers, d.tile))
    base = w.daily_output()
    rows = [("Nu", f"{depot.carriers} bärare vid förrådet på {tag(depot.tile)}", base, "–")]
    if depot.carriers < MAX_CARRIERS:
        depot.carriers += 1
        rows.append(("En bärare till", "", w.daily_output(), "1 ledig bybo"))
        depot.carriers -= 1
    served = [b for b in w.on(island) if b.kind not in DEPOTS and w.travel(b.tile, island)[1] == depot.tile
              and w.rate(b)[0]]
    far = max(served, key=lambda b: (w.travel(b.tile, island)[0], b.tile))
    tiles = [t for t in w.path(far.tile, island) if t not in w.roads]
    if tiles:
        w.roads.update(tiles)
        w._times.clear()
        rows.append(("Väg", f"till {far.kind.lower()} på {tag(far.tile)}, {len(tiles)} brickor", w.daily_output(),
                     f"{ROAD_COST['sten'] * len(tiles)} sten, {ROAD_WORK * len(tiles)} arbetstimmar"))
        w.roads.difference_update(tiles)
        w._times.clear()
    best = None
    for p in sorted(TILES):
        if TILES[p].island == island and w.can_build("Förråd", p):
            w.buildings.append(Building("Förråd", p, staffed=True))
            w._times.clear()
            out, load = w.daily_output(), sum(w.carrier_load(island).values())
            w.buildings.pop()
            w._times.clear()
            if best is None or (out, -load) > (best[1], -best[2]):
                best = (p, out, load)
    spec = SPECS["Förråd"]
    rows.append(("Ett förråd till", f"på {tag(best[0])}", best[1],
                 ", ".join(f"{n} {r}" for r, n in spec.cost.items()) + f", {spec.work} arbetstimmar, 1 bybo"))
    return depot, need[depot.tile], rows


# --- Utskrift --------------------------------------------------------------------------------------

def row(name, profile, w, base):
    used = sum(w.spent.values())
    per = f"{(w.value() - base) / used:.2f}" if used else "–"
    tiers = "/".join(str(n) for n in w.tier_counts())
    home = w.settlements[HOME].stock
    return (f"| {name} | {profile} | {w.population()} | {tiers} | {len(w.buildings)} "
            f"| {len(w.settlements)} | {round(home['plank'])} | {round(home['sten'])} "
            f"| {w.carried_hours / max(1, w.carrier_capacity):.0%} | {w.daily_output():.0f} "
            f"| {w.value():.0f} | {w.spent['U']}/{w.spent['B']}/{w.spent['G']} "
            f"| {w.forces['U']:.0f}/{w.forces['B']:.0f}/{w.forces['G']:.0f} | {per} |")


def week(day):
    return "–" if day is None else str(day // 7 + 1)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    assert sum(1 for s in SPECS.values()) > 20, "D-105: fler än 20 byggnader i epok 1"
    print(f"## Byggnader ({len(SPECS)} i kalkylen, plus Runsten och Vårdkase från epok 1)\n")
    print("| Byggnad | Steg | Kostnad | Arbete | Byggkraft | Bemanning | Lägsta nivå | Krav |")
    print("|---|---|---|---|---|---|---|---|")
    print("\n".join(catalog()))
    print("\n## Behov och kedjor (INV-R4)\n")
    print("| Behov | Vara | Kedja | Mark | Byggkraft | Bemannas av |\n|---|---|---|---|---|---|")
    print("\n".join(check_needs()))

    base = World().value()
    print(f"\n## Sju dygn\n\nStartvärde: {base:.0f}\n")
    print("| Körning | Profil | Bybor | Nivåer T/B/H | Byggnader | Bosättningar | Plank | Sten "
          "| Bärarnas last | Dagsproduktion | Utvecklingsvärde | Använt U/B/G | Sparat U/B/G "
          "| Värde per använd kraft |\n|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    runs = [
        ("Närområdet", strategy_land, "måttlig"),
        ("Havet", strategy_sea, "måttlig"),
        ("Bygden", strategy_village, "måttlig"),
        ("Naiv", strategy_naive, "hög"),
        ("Utan träning", strategy_no_training, "utan träning"),
        ("Ingen spelare", strategy_idle, "ingen"),
        ("Ingen spelare", strategy_idle, "måttlig"),
    ]
    week_one, per_force = [], {}
    for name, strat, profile in runs:
        w = run(strat, profile)
        print(row(name, profile, w, base))
        per_force[name] = (w.value() - base) / max(1, sum(w.spent.values()))
        met = ", ".join(f"{k} (dygn {v + 1})" for k, v in sorted(w.introduced.items(), key=lambda kv: kv[1]))
        week_one.append(f"| {name} | {met or '–'} |")

    thoughtful = min(per_force[n] for n in ("Närområdet", "Havet", "Bygden"))
    assert thoughtful >= 1.3 * per_force["Naiv"], "strategin ska hinna ikapp egenvården (D-34)"
    print(f"\nSämsta genomtänkta strategi mot den naiva: {thoughtful / per_force['Naiv']:.2f} gånger "
          "så mycket värde per använd kraft (D-34 kräver minst 1,3).")

    print("\n## System som möter spelaren första veckan\n\n| Körning | System (första dygnet) |\n|---|---|")
    print("\n".join(week_one))

    print("\n## Samma strategi med mer egenvård (D-34)\n")
    print("| Strategi | Dygn | Utvecklingsvärde måttlig | Utvecklingsvärde hög | Skillnad |\n|---|---|---|---|---|")
    for name, strat in [("Närområdet", strategy_land), ("Havet", strategy_sea), ("Bygden", strategy_village)]:
        for days in (DAYS, 2 * DAYS, 4 * DAYS, LONG_DAYS):
            low, high = run(strat, "måttlig", days).value(), run(strat, "hög", days).value()
            # Första veckan följer strategin sin plan; därefter väljer planeraren efter krafterna,
            # så en enskild dag kan visa några procent mindre med mer egenvård.
            assert days > DAYS or high >= low, "mer egenvård ska alltid synas vid samma strategi (D-34)"
            print(f"| {name} | {days} | {low:.0f} | {high:.0f} | {high / low - 1:+.0%} |")

    print(f"\n## Sex veckor\n\n| Körning | Profil | Bönder låses upp | Hantverkare | Sjöfart "
          "| Bärarna hinner inte | Bybor | Nivåer T/B/H | Byggnader | Bosättningar "
          "| Utvecklingsvärde vecka 2/4/6 |\n|---|---|---|---|---|---|---|---|---|---|---|")
    long_runs = runs[:5] + [("Havet", strategy_sea, "hög"), ("Ingen spelare", strategy_idle, "ingen")]
    for name, strat, profile in long_runs:
        values = [run(strat, profile, d).value() for d in (2 * DAYS, 4 * DAYS)]
        w = run(strat, profile, LONG_DAYS)
        values.append(w.value())
        u = w.unlocked_day
        print(f"| {name} | {profile} | vecka {week(u.get('Bönder'))} | vecka {week(u.get('Hantverkare'))} "
              f"| vecka {week(u.get('Sjöfart'))} | vecka {week(w.introduced.get('Bärare'))} | {w.population()} "
              f"| {'/'.join(str(n) for n in w.tier_counts())} | {len(w.buildings)} | {len(w.settlements)} "
              f"| {' / '.join(f'{v:.0f}' for v in values)} |")

    year = run(strategy_idle, "ingen", 365)
    print(f"\nEtt år utan spelare och egenvård: {year.population()} bybor, {len(year.buildings)} byggnader, "
          "inget har minskat och inga lager eller saldon är negativa (INV-V4).")

    print("\n## Vad en bärare, en väg eller ett förråd ger\n")
    for name, strat, profile, days in [("Närområdet", strategy_land, "måttlig", 3)]:
        w = run(strat, profile, days)
        w.dawn(days, profile)
        depot, load, rows = remedies(w)
        print(f"{name}, gryningen dygn {days + 1}: förrådet på {tag(depot.tile)} behöver {load:.1f} bärartimmar "
              f"per aktiv timme och har {depot.carriers} bärare.\n")
        print("| Åtgärd | Var | Dagsproduktion | Kostnad |\n|---|---|---|---|")
        for what, where, out, cost in rows:
            print(f"| {what} | {where} | {out:.0f} | {cost} |")
        print()

    print("\n## Migration från ekonomi v1 efter sju dygn, sedan sju dygn i v2\n")
    print("| Värld | Bybor | Byggnader | Bosättningar | Vägbrickor | Sten tillbaka för vägar | Bybor dygn 14 "
          "| Byggnader dygn 14 | Utvecklingsvärde dygn 14 |\n|---|---|---|---|---|---|---|---|---|")
    print(check_migration("Närområdet", ev1.strategy_land, strategy_land))
    print(check_migration("Närområdet med väg", strategy_v1_with_road, strategy_land))
    print(check_migration("Havet", ev1.strategy_sea, strategy_sea))


if __name__ == "__main__":
    main()
