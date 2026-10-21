"""Designkalkyl för ekonomi v1 (TASK-004).

Timsimulerar en värld i sju spelardygn för olika strategier och egenvårdsprofiler,
och kontrollerar att inget förfaller. Siffrorna motsvarar docs/design/economy-v1.md
och docs/design/forces.md.

Detta är ett räkneark i kodform, inte spelets simulering: den riktiga simuleringen
skrivs i C# med heltal/fixed-point (INV-D3). Kör: python -I economy_v1.py
"""

import sys
from dataclasses import dataclass, field

DAYS = 7
ACTIVE_HOURS = 16  # viloperiod 8 h; spelardygnet börjar vid gryning (timme 0)

# --- Resurser -----------------------------------------------------------------

RESOURCES = ["fisk", "säd", "timmer", "plank", "sten", "ull", "vadmal"]
FOOD = ["fisk", "säd"]
# Vikter för utvecklingsvärdet (endast balansmått, visas aldrig för spelaren).
WEIGHT = {"fisk": 1, "säd": 1, "timmer": 1, "plank": 3, "sten": 2, "ull": 2, "vadmal": 6}
VILLAGER_VALUE = 15
STORAGE_PER_DEPOT = 150  # per resurs och förråd

# --- Karta: brickor (ADR-0010, D-79) --------------------------------------------
# Varje landbricka är en möjlig byggplats, och vad den duger till följer av terräng och
# grannar (TASK-037). Kartan är prototypens handgjorda karta `prototype-home` (TASK-029):
# samma land, vatten och terräng som klienten ritar (client/.../Look/IslandMap.cs).

DEEP, SHALLOW, BEACH, GRASS, FOREST, FARMLAND, PASTURE, MOUNTAIN = (
    "djuphav", "grunt", "strand", "äng", "skog", "åker", "bete", "berg")
LAND = {BEACH, GRASS, FOREST, FARMLAND, PASTURE, MOUNTAIN}
NEIGHBOURS = [(1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)]


@dataclass
class Tile:
    terrain: str
    island: str = ""      # landbrickor och grunt vatten intill dem; tomt för djuphavet
    richness: float = 1.0  # rikedom: 1,0 är normalt (kartans procent / 100)
    hidden: bool = False   # under dimman tills en kartläggning eller expedition lyfter den


TILES = {}


def neighbours(pos):
    return [(pos[0] + dq, pos[1] + dr) for dq, dr in NEIGHBOURS]


def hexdist(a, b) -> int:
    dq, dr = a[0] - b[0], a[1] - b[1]
    return (abs(dq) + abs(dr) + abs(dq + dr)) // 2


def add_shores():
    """Hav intill land är grunt och hör till landets ö (IslandMap.cs)."""
    for pos, tile in TILES.items():
        if tile.terrain in (DEEP, SHALLOW):
            land = [TILES[n] for n in neighbours(pos) if n in TILES and TILES[n].terrain in LAND]
            if land:
                tile.terrain, tile.island = SHALLOW, land[0].island


def add_island(island, land, richness=None):
    """Lägger till en ö under dimman med grunt vatten runt och djuphav utanför."""
    for p, terrain in land.items():
        TILES[p] = Tile(terrain, island, hidden=True)
    for p in land:
        for n in neighbours(p):
            for m in [n] + neighbours(n):
                TILES.setdefault(m, Tile(DEEP))
    add_shores()
    for p, tile in TILES.items():
        if tile.island == island:
            tile.hidden = True
    for p, r in (richness or {}).items():
        TILES[p].richness = r


def prototype_home():
    """Hemön och Nyön som klienten ritar dem; det som var kuliss är riktig terräng (ADR-0010)."""
    named = {  # prototypens namngivna brickor med sin terräng (TASK-029)
        (0, 0): GRASS, (-1, 0): GRASS, (0, -1): GRASS, (-1, 1): GRASS, (2, -1): GRASS,
        (2, -2): GRASS, (1, 1): BEACH, (0, 1): FARMLAND, (-1, 2): FARMLAND, (1, -1): FOREST,
        (3, -2): FOREST, (-2, 0): FOREST, (-4, 0): MOUNTAIN, (3, -1): MOUNTAIN, (-2, -1): PASTURE,
        (10, 0): BEACH, (10, 1): PASTURE, (11, 0): MOUNTAIN,
        (-3, -1): MOUNTAIN,  # berget intill krönet, så att stenådern blir ett val (D-95)
    }
    water = {(1, 0), (2, 0), (3, 0), (4, 0), (4, -1), (-3, 3), (-3, 4), (-4, 4), (-2, 3),
             (0, -4), (1, -4), (4, -4), (-4, 1), (2, 2), (3, 1)}

    def scenery(q, r, ring):
        noise = (q * 73 + r * 151 + q * r * 17) & 7
        if ring == 4:
            return BEACH if noise < 5 else GRASS
        return FOREST if noise < 2 else GRASS

    for q in range(-9, 18):
        for r in range(-9, 10):
            d, to_nyon = hexdist((q, r), (0, 0)), hexdist((q, r), (10, 0))
            if d <= 4 and (q, r) not in water:
                TILES[(q, r)] = Tile(named.get((q, r)) or scenery(q, r, d), "Hemön")
            elif to_nyon <= 1 and (q, r) != (9, 0):  # viken väster om N0, där skeppen lägger till
                TILES[(q, r)] = Tile(named.get((q, r)) or scenery(q, r, 0), "Nyön", hidden=True)
            elif d <= 9 or to_nyon <= 7:
                TILES[(q, r)] = Tile(DEEP)
    add_shores()
    for pos, tile in TILES.items():
        if tile.island == "Nyön":
            tile.hidden = True
    # Rikedom och dimma: stenådern, urskogen och det rika fiskevattnet visas av kartläggningar.
    # Rikedomen är kartans (prototype-home-1.map): av urskogens två skogsbrickor är bara F3 rik.
    for pos, richness in {(3, -1): 2.0, (-2, 0): 1.25, (-3, 3): 1.5,
                          (10, 1): 2.0, (11, 0): 2.0}.items():
        TILES[pos].richness = richness
    for area in SURVEYS:
        for pos in area:
            TILES[pos].hidden = True


SURVEYS = [[(3, -1), (-2, 0), (-3, 1)], [(-3, 3), (-1, 2)]]  # närområdet, sedan kusten längre bort
HOME = "Hemön"
prototype_home()

# Krönet är startöns enda högsta bricka och bär bara Vårdkasen, även när det är ett berg (D-92).
# I prototype-home-1.map är (-4,0) högst, med höjd 6 mot stenåderns 5 på (3,-1), som förblir
# stenbrottets.
CREST = (-4, 0)

# Prototypens gamla namn på brickorna, så att strategierna läses som förut (TASK-029).
NAMES = {
    "S": (0, 0), "L1": (-1, 0), "L2": (0, -1), "L3": (-1, 1), "K1": (1, 0), "K2": (-3, 3),
    "H1": (1, 1), "O1": (0, 1), "O2": (-1, 2), "F1": (1, -1), "F2": (3, -2), "F3": (-2, 0),
    "E1": (2, -1), "E2": (2, -2), "M1": (-4, 0), "M2": (3, -1), "M3": (-3, -1), "P1": (-2, -1),
    "N0": (10, 0), "N1": (10, 1), "N2": (11, 0),
    "N9": (9, 1),  # Nyöns hamnläge intill betet
}


def coord(tile):
    """Koordinaterna för en bricka, given med sitt namn eller sina koordinater."""
    return NAMES.get(tile, tile)


# --- Vad en bricka duger till (D-79) ------------------------------------------------

def reaches_deep_sea(start) -> bool:
    seen, todo = {start}, [start]
    while todo:
        p = todo.pop()
        if TILES[p].terrain == DEEP:
            return True
        for n in neighbours(p):
            if n in TILES and n not in seen and TILES[n].terrain in (DEEP, SHALLOW):
                seen.add(n)
                todo.append(n)
    return False


def reach(p):
    """Brickorna en huggarkoja når: sin egen och de sex grannarna."""
    return [p] + [n for n in neighbours(p) if n in TILES]


def uses(p) -> set:
    """Vad brickan duger till enligt kartan, härlett ur terräng, höjd och grannar.

    Skogskravet gäller också världens tillstånd: se World.meets (D-93).
    """
    t = TILES[p]
    if p == CREST:
        return {"krön"}  # krönet bär bara Vårdkasen (D-92)
    if t.terrain == SHALLOW:
        return {"fiskevatten"}  # grunt vatten intill land; fiskeboden står på pålar
    if t.terrain not in LAND:
        return set()
    if t.terrain == MOUNTAIN:
        return {"berg"}  # berget bär bara stenbrott
    out = {"land"}
    if any(TILES[n].terrain == FOREST for n in reach(p)):
        out.add("skog")
    if t.terrain == FARMLAND:
        out.add("åker")
    if t.terrain == PASTURE:
        out.add("bete")
    if any(n in TILES and TILES[n].terrain == SHALLOW and reaches_deep_sea(n) for n in neighbours(p)):
        out.add("hamnläge")
    return out


def distance_factor(d: int, road: bool) -> float:
    cls = 0 if d <= 1 else 1 if d <= 3 else 2
    if road:
        cls = max(0, cls - 1)
    return [1.0, 0.75, 0.5][cls]

# --- Byggnader ------------------------------------------------------------------

@dataclass(frozen=True)
class Spec:
    cost: dict
    work: int  # arbetstimmar
    build_power: int
    staff: int
    requirement: str


SPECS = {
    "Långhus": Spec({"timmer": 10, "plank": 15, "sten": 10}, 40, 60, 0, "land"),
    "Stuga": Spec({"timmer": 15, "sten": 5}, 32, 0, 0, "land"),  # bostad utan kraft (D-43)
    "Förråd": Spec({"timmer": 10, "sten": 5}, 16, 0, 1, "land"),
    "Fiskebod": Spec({"timmer": 8}, 12, 0, 2, "fiskevatten"),
    "Åker": Spec({"timmer": 4}, 8, 0, 2, "åker"),
    "Huggarkoja": Spec({"timmer": 6}, 8, 0, 2, "skog"),
    "Fårgård": Spec({"timmer": 10, "plank": 5}, 12, 0, 1, "bete"),
    "Sågverk": Spec({"timmer": 15, "sten": 5}, 24, 30, 2, "land"),
    "Stenbrott": Spec({"timmer": 10, "plank": 4}, 16, 0, 2, "berg"),
    "Väveri": Spec({"plank": 10, "sten": 5}, 24, 60, 2, "land"),
    "Brygga": Spec({"timmer": 15, "plank": 10}, 24, 60, 1, "hamnläge"),
    "Utkiksvarde": Spec({"sten": 10}, 12, 40, 0, "land"),
}
UPGRADE = Spec({"plank": 10, "sten": 5}, 16, 50, 0, "")
ROAD = Spec({"sten": 2}, 4, 0, 0, "")
HOUSING = {"Långhus": 8, "Stuga": 4}
START_HOUSING = 16

# --- Krafter (docs/design/forces.md) ------------------------------------------

CAPS = {"U": (400, 800), "B": (600, 1200), "G": (300, 600)}


def credit(balance: float, amount: float, force: str) -> float:
    full, half = CAPS[force]
    while amount > 0:
        if balance < full:
            step = min(amount, full - balance)
            balance, amount = balance + step, amount - step
        elif balance < half:
            step = min(amount, 2 * (half - balance))
            balance, amount = balance + step / 2, amount - step
        else:
            break
    return balance


PROFILES = {
    # Utforskning/dygn, Gryningskraft/dygn, Byggkraft per spelardygn (rullande vecka).
    "måttlig": {"U": 86, "G": 92, "B": [100, 0, 90, 0, 80, 0, 0]},
    "hög": {"U": 103, "G": 98, "B": [100, 90, 80, 50, 25, 0, 0]},
    "ingen": {"U": 0, "G": 40, "B": [0] * 7},  # saknad sömndata ger grundnivå
    "utan träning": {"U": 86, "G": 92, "B": [0] * 7},  # vanan träning bortvald (HLT-01)
}
START_FORCES = {"U": 100, "B": 100, "G": 40}  # guidad tur (D-45)

DAWN_WORKDAY_COST = 30   # Dagsverke: +50 % för en byggnad i ett spelardygn
DAWN_RUSH_COST = 20      # Rusta: +8 arbetstimmar på ett bygge
SURVEY_COSTS = [60, 120]
EXPEDITION_COST = 250
EXPEDITION_HOURS = 32
# Expeditionsmål i den ordning de hittas: (ö, Utforskning, aktiva timmar); hela ön visas.
EXPEDITIONS = [("Nyön", EXPEDITION_COST, EXPEDITION_HOURS)]
SEA_CAPACITY_PER_DAY = 40
RESERVE = 20  # minimilager som förädling lämnar kvar (LOG-06), en gratis planeringsinställning

# --- Värld ----------------------------------------------------------------------

FOREST_PER_TILE = 40.0     # timmer en skogsbricka rymmer
FOREST_REGROWTH = 0.25     # timmer per timme och skogsbricka
WOODCUTTER_PER_TILE = 0.5  # timmer per timme och skogsbricka som fälls
WOODCUTTER_TILES = 2       # skogsbrickor en huggarkoja fäller samtidigt


def tag(p) -> str:
    return f"{p[0]},{p[1]}"


@dataclass
class Building:
    kind: str
    tile: tuple  # brickans axiala koordinater
    level: int = 1
    staffed: bool = False
    road: bool = False
    buffer: float = 0.0  # långsam process: mognad säd eller ull som väntar på skörd


@dataclass
class Project:
    label: str
    work_left: float
    on_done: object


@dataclass
class World:
    stock: dict = field(default_factory=lambda: {
        "fisk": 30, "säd": 0, "timmer": 40, "plank": 20, "sten": 20, "ull": 0, "vadmal": 0})
    villagers: int = 12
    buildings: list = field(default_factory=list)
    projects: list = field(default_factory=list)
    forces: dict = field(default_factory=lambda: dict(START_FORCES))
    revealed: set = field(default_factory=set)
    forest: dict = field(default_factory=dict)  # timmer kvar per skogsbricka
    remote_stock: dict = field(default_factory=dict)  # lager på andra öar än Hemön
    expedition_left: float = 0.0
    expedition_target: int = 0  # index i EXPEDITIONS för nästa eller pågående expedition
    boosted: set = field(default_factory=set)
    log: list = field(default_factory=list)
    credited: dict = field(default_factory=lambda: {"U": 0, "B": 0, "G": 0})
    spent: dict = field(default_factory=lambda: {"U": 0, "B": 0, "G": 0})

    def __post_init__(self):
        self.forest = {p: FOREST_PER_TILE for p, t in TILES.items() if t.terrain == FOREST}
        self.revealed = {p for p, t in TILES.items() if not t.hidden}
        self._distances = {}
        for kind, tile in [("Förråd", "S"), ("Fiskebod", "K1"), ("Åker", "O1"), ("Huggarkoja", "F1")]:
            self.buildings.append(Building(kind, coord(tile)))
        self.staff()

    # -- hjälpare --
    def has(self, kind, island=None):
        return any(b.kind == kind and (island is None or TILES[b.tile].island == island)
                   for b in self.buildings)

    def housing(self):
        return START_HOUSING + sum(HOUSING.get(b.kind, 0) for b in self.buildings)

    def depots(self, island):
        return tuple(sorted(b.tile for b in self.buildings
                            if TILES[b.tile].island == island and b.kind in ("Förråd", "Brygga")))

    def factor(self, b):
        return distance_factor(self.depot_distance(b.tile), b.road)

    def depot_distance(self, tile):
        """Steg över land till närmaste förråd eller brygga på samma ö (sökväg, TASK-028)."""
        p = coord(tile)
        island = TILES[p].island
        key = (p, self.depots(island))
        if key not in self._distances:
            goals, seen, frontier, d = set(key[1]), {p}, [p], 0
            while frontier and not goals & set(frontier):
                d += 1
                step = []
                for f in frontier:
                    for n in neighbours(f):
                        if (n in TILES and n not in seen and TILES[n].island == island
                                and TILES[n].terrain in LAND):
                            seen.add(n)
                            step.append(n)
                frontier = step
            self._distances[key] = d if frontier else 99
        return self._distances[key]

    def building_at(self, p):
        return next((b for b in self.buildings if b.tile == p), None)

    def woods(self, p):
        """Skogsbrickor som en huggarkoja på p fäller, rikast först; bebyggd skog fälls inte."""
        tiles = [n for n in reach(p) if TILES[n].terrain == FOREST and n in self.revealed
                 and (n == p or self.building_at(n) is None)]
        return sorted(tiles, key=lambda n: (-TILES[n].richness, -self.forest[n], n))

    def pending(self, label):
        return any(p.label == label for p in self.projects)

    def storage_cap(self):
        return STORAGE_PER_DEPOT * sum(1 for b in self.buildings if b.kind == "Förråd")

    def free_villagers(self):
        return self.villagers - sum(SPECS[b.kind].staff for b in self.buildings if b.staffed)

    def staff(self):
        for b in self.buildings:
            need = SPECS[b.kind].staff
            if not b.staffed and need and self.free_villagers() >= need:
                b.staffed = True

    def occupied(self, tile):
        p = coord(tile)
        return self.building_at(p) is not None or any(
            pr.label.endswith("@" + tag(p)) for pr in self.projects)

    def meets(self, p, requirement):
        """Uppfyller brickan kravet nu? En huggarkoja kräver skog att fälla (D-93)."""
        if requirement not in uses(p):
            return False
        return requirement != "skog" or bool(self.woods(p))

    def can_build(self, kind, tile):
        """Byggregeln (D-79): brickan är upptäckt, ledig och uppfyller byggnadens krav."""
        p = coord(tile)
        return (p in TILES and p in self.revealed and self.meets(p, SPECS[kind].requirement)
                and not self.occupied(p))

    def pay(self, cost, bp):
        if any(self.stock[r] < n for r, n in cost.items()) or self.forces["B"] < bp:
            return False
        for r, n in cost.items():
            self.stock[r] -= n
        self.forces["B"] -= bp
        self.spent["B"] += bp
        return True

    # -- kommandon (nekas utan sidoeffekter, INV-D7) --
    def build(self, kind, tile):
        spec, p = SPECS[kind], coord(tile)
        if not self.can_build(kind, p):
            return False
        island = TILES[p].island
        if island != HOME and kind != "Brygga" and not self.has("Brygga", island):
            return False
        if not self.pay(spec.cost, spec.build_power):
            return False
        self.projects.append(Project(f"{kind}@{tag(p)}", spec.work,
                                     lambda: self.buildings.append(Building(kind, p))))
        return True

    def upgrade(self, kind, tile):
        p = coord(tile)
        b = next((b for b in self.buildings if b.kind == kind and b.tile == p), None)
        label = f"Uppgradera {kind}@{tag(p)}"
        if b is None or b.level > 1 or self.pending(label):
            return False
        if not self.pay(UPGRADE.cost, UPGRADE.build_power):
            return False
        self.projects.append(Project(label, UPGRADE.work,
                                     lambda: setattr(b, "level", 2)))
        return True

    def road(self, kind, tile):
        p = coord(tile)
        b = next((b for b in self.buildings if b.kind == kind and b.tile == p), None)
        label = f"Väg till {kind}@{tag(p)}"
        if b is None or b.road or self.pending(label):
            return False
        d = self.depot_distance(p)
        if not self.pay({"sten": ROAD.cost["sten"] * d}, 0):
            return False
        self.projects.append(Project(label, ROAD.work * d,
                                     lambda: setattr(b, "road", True)))
        return True

    def spend(self, force, amount):
        if self.forces[force] < amount:
            return False
        self.forces[force] -= amount
        self.spent[force] += amount
        return True

    def explore_cost(self, base):
        return base * 3 // 4 if self.has("Utkiksvarde") else base

    def survey(self):
        i = next((i for i, area in enumerate(SURVEYS)
                  if not all(s in self.revealed for s in area)), None)
        if i is None or not self.spend("U", self.explore_cost(SURVEY_COSTS[i])):
            return False
        self.revealed.update(SURVEYS[i])
        return True

    def expedition(self):
        if not self.has("Brygga") or self.expedition_left or self.expedition_target >= len(EXPEDITIONS):
            return False
        _, cost, hours = EXPEDITIONS[self.expedition_target]
        if not self.spend("U", self.explore_cost(cost)):
            return False
        self.expedition_left = hours
        return True

    def workday(self, kind, tile):
        p = coord(tile)
        if not any(b.kind == kind and b.tile == p and b.staffed for b in self.buildings):
            return False
        if (kind, p) in self.boosted or not self.spend("G", DAWN_WORKDAY_COST):
            return False
        self.boosted.add((kind, p))
        return True

    def rush(self):
        if not self.projects or not self.spend("G", DAWN_RUSH_COST):
            return False
        self.projects[0].work_left = max(0, self.projects[0].work_left - 8)
        return True

    # -- tid --
    def dawn(self, day, profile):
        p = PROFILES[profile]
        for f, amount in (("U", p["U"]), ("G", p["G"]), ("B", p["B"][day])):
            before = self.forces[f]
            self.forces[f] = credit(before, amount, f)
            self.credited[f] += self.forces[f] - before
        self.boosted = set()
        # Basbehov: varje bybo äter 1 mat per spelardygn; brist stoppar bara tillväxt (INV-V3).
        need = self.villagers
        for r in FOOD:
            take = min(self.stock[r], need)
            self.stock[r] -= take
            need -= take
        fed = need == 0
        comfort = fed and self.stock["vadmal"] >= 2
        if comfort:
            self.stock["vadmal"] -= 2
        growth = (1 if fed else 0) + (1 if comfort else 0)
        self.villagers = min(self.housing(), self.villagers + growth)
        self.staff()

    def hour(self, active):
        # Långsamma processer fortgår dygnet runt (INV-T5).
        for p in self.forest:
            self.forest[p] = min(FOREST_PER_TILE, self.forest[p] + FOREST_REGROWTH)
        for b in self.buildings:
            if b.kind == "Åker":
                b.buffer += 0.5 * TILES[b.tile].richness
            if b.kind == "Fårgård":
                b.buffer += 0.4 * TILES[b.tile].richness
        if not active:
            return  # byborna sover (INV-T4)
        if self.expedition_left:
            self.expedition_left -= 1
            if self.expedition_left == 0:
                island, _, _ = EXPEDITIONS[self.expedition_target]
                self.expedition_target += 1
                self.revealed.update(p for p, t in TILES.items() if t.island == island)
                self.remote_stock[island] = {r: 0.0 for r in RESOURCES}
                self.log.append(f"Expeditionen hittade {island}")
        # Byggen: lediga bybor arbetar.
        labour = float(self.free_villagers())
        while labour > 0 and self.projects:
            p = self.projects[0]
            used = min(labour, p.work_left)
            p.work_left -= used
            labour -= used
            if p.work_left <= 0:
                self.projects.pop(0)
                p.on_done()
                self.staff()
        # Produktion.
        cap = self.storage_cap()
        for b in self.buildings:
            if not b.staffed:
                continue
            k = self.factor(b) * (1.5 if b.level == 2 else 1.0) * (
                1.5 if (b.kind, b.tile) in self.boosted else 1.0)
            tile = TILES[b.tile]
            out = self.stock if tile.island == HOME else self.remote_stock[tile.island]
            if b.kind == "Fiskebod":
                self.add(out, "fisk", 1.25 * tile.richness * k, cap)
            elif b.kind in ("Åker", "Fårgård"):
                res = "säd" if b.kind == "Åker" else "ull"
                take = min(b.buffer, 2.0 * k)
                b.buffer -= self.add(out, res, take, cap)
            elif b.kind == "Huggarkoja":
                felled = [n for n in self.woods(b.tile) if self.forest[n] > 0][:WOODCUTTER_TILES]
                for n in felled:
                    take = min(WOODCUTTER_PER_TILE * TILES[n].richness * k, self.forest[n])
                    self.forest[n] -= self.add(out, "timmer", take, cap)
            elif b.kind == "Stenbrott":
                self.add(out, "sten", 0.75 * tile.richness * k, cap)
            elif b.kind == "Sågverk":
                n = min(1.0 * k, max(0.0, self.stock["timmer"] - RESERVE) / 2)
                n = self.add(out, "plank", n, cap)
                self.stock["timmer"] -= 2 * n
            elif b.kind == "Väveri":
                n = min(0.5 * k, max(0.0, self.stock["ull"] - RESERVE) / 2)
                n = self.add(out, "vadmal", n, cap)
                self.stock["ull"] -= 2 * n
        # Sjövägar till Hemön, en per ö med brygga i båda ändar.
        for island, remote in self.remote_stock.items():
            if not (self.has("Brygga", island) and self.has("Brygga", HOME)):
                continue
            room = SEA_CAPACITY_PER_DAY / ACTIVE_HOURS
            for r in RESOURCES:
                moved = self.add(self.stock, r, min(room, remote[r]), cap)
                remote[r] -= moved
                room -= moved

    @staticmethod
    def add(store, res, amount, cap):
        amount = max(0.0, min(amount, cap - store[res]))  # fullt lager stoppar, inget går förlorat
        store[res] += amount
        return amount

    # -- mått --
    def woodcutter_rate(self, p):
        return sum(WOODCUTTER_PER_TILE * TILES[n].richness for n in self.woods(p)[:WOODCUTTER_TILES])

    def daily_output(self):
        """Effektiv produktionskapacitet per spelardygn, viktad (balansmått)."""
        total = 0.0
        for b in self.buildings:
            if not b.staffed:
                continue
            k = self.factor(b) * (1.5 if b.level == 2 else 1.0)
            richness = TILES[b.tile].richness
            per_hour = {
                "Fiskebod": ("fisk", 1.25 * richness),
                "Åker": ("säd", 0.5 * richness * 24 / ACTIVE_HOURS),
                "Fårgård": ("ull", 0.4 * richness * 24 / ACTIVE_HOURS),
                "Huggarkoja": ("timmer", self.woodcutter_rate(b.tile)),
                "Stenbrott": ("sten", 0.75 * richness),
                "Sågverk": ("plank", 1.0),
                "Väveri": ("vadmal", 0.5),
            }.get(b.kind)
            if per_hour:
                res, rate = per_hour
                total += WEIGHT[res] * rate * k * ACTIVE_HOURS
        return total

    def snapshot(self):
        return {
            "bybor": self.villagers,
            "byggnader": len(self.buildings),
            "nivå 2": sum(b.level == 2 for b in self.buildings),
            "öar": len({TILES[b.tile].island for b in self.buildings}),
            "lager": {r: round(v) for r, v in self.stock.items()},
        }

    def value(self):
        stock = sum(WEIGHT[r] * v for r, v in self.stock.items())
        return self.villagers * VILLAGER_VALUE + stock / 2 + 3 * self.daily_output()

# --- Strategier -------------------------------------------------------------------

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


def strategy_sea(w: World, day: int):
    """Havet: brygga, expedition till Nyön, ull och vadmal för snabbare tillväxt."""
    w.build("Brygga", "H1")
    w.build("Huggarkoja", "E2")
    w.build("Sågverk", "L3")
    w.expedition()
    w.build("Långhus", "L1")
    w.build("Brygga", "N9")
    w.build("Fårgård", "N1")
    w.build("Stenbrott", "N2")
    w.build("Väveri", "L2")
    w.build("Långhus", "E1")
    w.workday("Sågverk", "L3")
    w.workday("Fårgård", "N1")
    w.rush()


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
    """Utan träning: bara bostäder och produktion som inte kostar Byggkraft (D-43)."""
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


def strategy_idle(w: World, day: int):
    """Ingen spelare: inga kommandon alls."""


# --- Körning ------------------------------------------------------------------------

def run(strategy, profile):
    w = World()
    monotone = lambda: (w.villagers, len(w.buildings), sum(b.level for b in w.buildings),
                        len(w.revealed))
    for day in range(DAYS):
        w.dawn(day, profile)
        strategy(w, day)
        for h in range(24):
            before = monotone()
            w.hour(active=h < ACTIVE_HOURS)
            after = monotone()
            assert all(a >= b for a, b in zip(after, before)), "förfall (INV-V1)"
            assert all(v >= 0 for v in w.stock.values()), "negativt lager (INV-R1)"
            assert all(v >= 0 for v in w.forces.values()), "negativt saldo (INV-F1)"
    return w


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    runs = [
        ("Närområdet", strategy_land, "måttlig"),
        ("Havet", strategy_sea, "måttlig"),
        ("Naiv", strategy_naive, "hög"),
        ("Utan träning", strategy_no_training, "utan träning"),
        ("Ingen spelare, ingen egenvård", strategy_idle, "ingen"),
        ("Ingen spelare, måttlig egenvård", strategy_idle, "måttlig"),
    ]
    print("Byggplatser per ö: brickor som duger till varje krav (D-79)\n")
    kinds = ["land", "skog", "åker", "bete", "berg", "krön", "fiskevatten", "hamnläge"]
    print("| Ö | Landbrickor | " + " | ".join(kinds) + " |")
    print("|---|---|" + "---|" * len(kinds))
    for island in [HOME] + [e[0] for e in EXPEDITIONS]:
        tiles = [p for p, t in TILES.items() if t.island == island]
        land = sum(TILES[p].terrain in LAND for p in tiles)
        print(f"| {island} | {land} | " + " | ".join(
            str(sum(k in uses(p) for p in tiles)) for k in kinds) + " |")
    base = World().value()
    print(f"\nStartvärde: {base:.0f}\n")
    print("| Körning | Profil | Bybor | Byggnader | Nivå 2 | Öar | Plank | Sten | Vadmal "
          "| Dagsproduktion | Utvecklingsvärde | Använt U/B/G | Sparat U/B/G | Värde per använd kraft |")
    print("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for name, strat, profile in runs:
        w = run(strat, profile)
        s = w.snapshot()
        used = sum(w.spent.values())
        gain = w.value() - base
        per = f"{gain / used:.2f}" if used else "–"
        print(f"| {name} | {profile} | {s['bybor']} | {s['byggnader']} | {s['nivå 2']} | {s['öar']} "
              f"| {s['lager']['plank']} | {s['lager']['sten']} | {s['lager']['vadmal']} "
              f"| {w.daily_output():.0f} | {w.value():.0f} "
              f"| {w.spent['U']}/{w.spent['B']}/{w.spent['G']} "
              f"| {w.forces['U']:.0f}/{w.forces['B']:.0f}/{w.forces['G']:.0f} | {per} |")
        for line in w.log:
            print(f"<!-- {name}: {line} -->")


if __name__ == "__main__":
    main()
