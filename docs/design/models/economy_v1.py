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

# --- Karta (axiala hexkoordinater) ---------------------------------------------

@dataclass
class Site:
    q: int
    r: int
    island: str
    kinds: set
    hidden: bool = False
    forest_tiles: int = 0
    richness: float = 1.0


SITES = {
    "S": Site(0, 0, "Hemön", {"land"}),
    "L1": Site(-1, 0, "Hemön", {"land"}),
    "L2": Site(0, -1, "Hemön", {"land"}),
    "L3": Site(-1, 1, "Hemön", {"land"}),
    "K1": Site(1, 0, "Hemön", {"kust", "fiskevatten"}),
    "K2": Site(-3, 3, "Hemön", {"kust", "fiskevatten"}, hidden=True, richness=1.5),
    "H1": Site(1, 1, "Hemön", {"kust", "hamnläge"}),
    "O1": Site(0, 1, "Hemön", {"odlingsmark"}),
    "O2": Site(-1, 2, "Hemön", {"odlingsmark"}),
    "F1": Site(1, -1, "Hemön", {"skog"}, forest_tiles=1),
    "F2": Site(3, -2, "Hemön", {"skog"}, forest_tiles=3),
    "E1": Site(2, -1, "Hemön", {"land"}),
    "E2": Site(2, -2, "Hemön", {"land"}),
    "M1": Site(-4, 0, "Hemön", {"berg"}),
    "M2": Site(3, -1, "Hemön", {"berg"}, hidden=True, richness=2.0),
    "F3": Site(-2, 0, "Hemön", {"skog"}, hidden=True, forest_tiles=4, richness=1.25),
    "P1": Site(-2, -1, "Hemön", {"bete"}),
    # Nyön: hittas av en expedition; varor skeppas via sjöväg till Hemön.
    "N0": Site(10, 0, "Nyön", {"kust", "hamnläge", "land"}, hidden=True),
    "N1": Site(10, 1, "Nyön", {"bete"}, hidden=True, richness=2.0),
    "N2": Site(11, 0, "Nyön", {"berg"}, hidden=True, richness=2.0),
}
SURVEYS = [["M2", "F3"], ["K2", "O2"]]  # närområdet, sedan kusten längre bort
HOME = "Hemön"


def hexdist(a: Site, b: Site) -> int:
    dq, dr = a.q - b.q, a.r - b.r
    return (abs(dq) + abs(dr) + abs(dq + dr)) // 2


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
    site_kind: str


SPECS = {
    "Långhus": Spec({"timmer": 10, "plank": 15, "sten": 10}, 40, 60, 0, "land"),
    "Stuga": Spec({"timmer": 15, "sten": 5}, 32, 0, 0, "land"),  # bostad utan kraft (D-43)
    "Förråd": Spec({"timmer": 10, "sten": 5}, 16, 0, 1, "land"),
    "Fiskebod": Spec({"timmer": 8}, 12, 0, 2, "fiskevatten"),
    "Åker": Spec({"timmer": 4}, 8, 0, 2, "odlingsmark"),
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
START_FORCES = {"U": 100, "B": 100, "G": 40}  # guidad tur [FÖRSLAG]

DAWN_WORKDAY_COST = 30   # Dagsverke: +50 % för en byggnad i ett spelardygn
DAWN_RUSH_COST = 20      # Rusta: +8 arbetstimmar på ett bygge
SURVEY_COSTS = [60, 120]
EXPEDITION_COST = 250
EXPEDITION_HOURS = 32
# Expeditionsmål i den ordning de hittas: (ö, Utforskning, aktiva timmar, platser som visas).
EXPEDITIONS = [("Nyön", EXPEDITION_COST, EXPEDITION_HOURS, ["N0", "N1", "N2"])]
SEA_CAPACITY_PER_DAY = 40
RESERVE = 20  # minimilager som förädling lämnar kvar (LOG-06), en gratis planeringsinställning

# --- Värld ----------------------------------------------------------------------

@dataclass
class Building:
    kind: str
    site: str
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
    forest: dict = field(default_factory=dict)
    remote_stock: dict = field(default_factory=dict)  # lager på andra öar än Hemön
    expedition_left: float = 0.0
    expedition_target: int = 0  # index i EXPEDITIONS för nästa eller pågående expedition
    boosted: set = field(default_factory=set)
    log: list = field(default_factory=list)
    credited: dict = field(default_factory=lambda: {"U": 0, "B": 0, "G": 0})
    spent: dict = field(default_factory=lambda: {"U": 0, "B": 0, "G": 0})

    def __post_init__(self):
        for name, s in SITES.items():
            if s.forest_tiles:
                self.forest[name] = 40.0 * s.forest_tiles
        self.revealed = {n for n, s in SITES.items() if not s.hidden}
        for kind, site in [("Förråd", "S"), ("Fiskebod", "K1"), ("Åker", "O1"), ("Huggarkoja", "F1")]:
            self.buildings.append(Building(kind, site))
        self.staff()

    # -- hjälpare --
    def has(self, kind, island=None):
        return any(b.kind == kind and (island is None or SITES[b.site].island == island)
                   for b in self.buildings)

    def housing(self):
        return START_HOUSING + sum(HOUSING.get(b.kind, 0) for b in self.buildings)

    def depots(self, island):
        return [SITES[b.site] for b in self.buildings
                if SITES[b.site].island == island and b.kind in ("Förråd", "Brygga")]

    def factor(self, b):
        site = SITES[b.site]
        if site.island != HOME:
            return 1.0
        return distance_factor(self.depot_distance(b.site), b.road)

    def depot_distance(self, site):
        return min(hexdist(SITES[site], dep) for dep in self.depots(HOME))

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

    def occupied(self, site):
        return any(b.site == site for b in self.buildings) or any(
            p.label.endswith("@" + site) for p in self.projects)

    def pay(self, cost, bp):
        if any(self.stock[r] < n for r, n in cost.items()) or self.forces["B"] < bp:
            return False
        for r, n in cost.items():
            self.stock[r] -= n
        self.forces["B"] -= bp
        self.spent["B"] += bp
        return True

    # -- kommandon (nekas utan sidoeffekter, INV-D7) --
    def build(self, kind, site):
        spec, s = SPECS[kind], SITES[site]
        if site not in self.revealed or spec.site_kind not in s.kinds or self.occupied(site):
            return False
        if s.island != HOME and kind != "Brygga" and not self.has("Brygga", s.island):
            return False
        if not self.pay(spec.cost, spec.build_power):
            return False
        self.projects.append(Project(f"{kind}@{site}", spec.work,
                                     lambda: self.buildings.append(Building(kind, site))))
        return True

    def upgrade(self, kind, site):
        b = next((b for b in self.buildings if b.kind == kind and b.site == site), None)
        label = f"Uppgradera {kind}@{site}"
        if b is None or b.level > 1 or self.pending(label):
            return False
        if not self.pay(UPGRADE.cost, UPGRADE.build_power):
            return False
        self.projects.append(Project(label, UPGRADE.work,
                                     lambda: setattr(b, "level", 2)))
        return True

    def road(self, kind, site):
        b = next((b for b in self.buildings if b.kind == kind and b.site == site), None)
        label = f"Väg till {kind}@{site}"
        if b is None or b.road or self.pending(label):
            return False
        d = self.depot_distance(site)
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
        _, cost, hours, _ = EXPEDITIONS[self.expedition_target]
        if not self.spend("U", self.explore_cost(cost)):
            return False
        self.expedition_left = hours
        return True

    def workday(self, kind, site):
        if not any(b.kind == kind and b.site == site and b.staffed for b in self.buildings):
            return False
        if (kind, site) in self.boosted or not self.spend("G", DAWN_WORKDAY_COST):
            return False
        self.boosted.add((kind, site))
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
        for name in self.forest:
            cap = 40.0 * SITES[name].forest_tiles
            self.forest[name] = min(cap, self.forest[name] + 0.25 * SITES[name].forest_tiles)
        for b in self.buildings:
            if b.kind == "Åker":
                b.buffer += 0.5 * SITES[b.site].richness
            if b.kind == "Fårgård":
                b.buffer += 0.4 * SITES[b.site].richness
        if not active:
            return  # byborna sover (INV-T4)
        if self.expedition_left:
            self.expedition_left -= 1
            if self.expedition_left == 0:
                island, _, _, sites = EXPEDITIONS[self.expedition_target]
                self.expedition_target += 1
                self.revealed.update(sites)
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
                1.5 if (b.kind, b.site) in self.boosted else 1.0)
            site = SITES[b.site]
            out = self.stock if site.island == HOME else self.remote_stock[site.island]
            if b.kind == "Fiskebod":
                self.add(out, "fisk", 1.25 * site.richness * k, cap)
            elif b.kind in ("Åker", "Fårgård"):
                res = "säd" if b.kind == "Åker" else "ull"
                take = min(b.buffer, 2.0 * k)
                b.buffer -= self.add(out, res, take, cap)
            elif b.kind == "Huggarkoja":
                rate = (1.0 if site.forest_tiles >= 2 else 0.5) * site.richness * k
                take = min(rate, self.forest[b.site])
                self.forest[b.site] -= self.add(out, "timmer", take, cap)
            elif b.kind == "Stenbrott":
                self.add(out, "sten", 0.75 * site.richness * k, cap)
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
    def daily_output(self):
        """Effektiv produktionskapacitet per spelardygn, viktad (balansmått)."""
        total = 0.0
        for b in self.buildings:
            if not b.staffed:
                continue
            k = self.factor(b) * (1.5 if b.level == 2 else 1.0)
            s = SITES[b.site]
            per_hour = {
                "Fiskebod": ("fisk", 1.25 * s.richness),
                "Åker": ("säd", 0.5 * s.richness * 24 / ACTIVE_HOURS),
                "Fårgård": ("ull", 0.4 * s.richness * 24 / ACTIVE_HOURS),
                "Huggarkoja": ("timmer", (1.0 if s.forest_tiles >= 2 else 0.5) * s.richness),
                "Stenbrott": ("sten", 0.75 * s.richness),
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
            "öar": len({SITES[b.site].island for b in self.buildings}),
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
    w.build("Huggarkoja", "F2")
    w.build("Sågverk", "L3")
    w.expedition()
    w.build("Långhus", "L1")
    w.build("Brygga", "N0")
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
    w.build("Stenbrott", "M1")
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
    base = World().value()
    print(f"Startvärde: {base:.0f}\n")
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
