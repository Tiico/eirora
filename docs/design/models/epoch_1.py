"""Tempokalkyl för epok 1 (TASK-011).

Timsimulerar en värld från den guidade turen fram till epokens milstolpe, att Vårdkasen
tänds, med samma motor som ekonomi v1 (economy_v1.py) och samma genomtänkta strategi
för alla egenvårdsprofiler. Krafterna räknas från vanorna med kurvorna i
docs/design/forces.md, dag för dag. Siffrorna motsvarar
docs/design/epoch-1.md.

Detta är ett räkneark i kodform, inte spelets simulering. Kör: python -I epoch_1.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # -I tar bort skriptets katalog

import economy_v1 as ev1  # noqa: E402
from economy_v1 import (  # noqa: E402
    ACTIVE_HOURS, BEACH, FARMLAND, FOREST, GRASS, NAMES, SPECS, TILES, Building, Project, Spec, World,
    add_island, coord)

MAX_WEEKS = 20
WEEK = 7

# --- Nytt innehåll i epok 1 [FÖRSLAG] -------------------------------------------
# Fler rutor att bygga på, rikt fiskevatten vid Nyön och Ytterön som andra
# expeditionens mål. Var Ytterön ligger avgör kartgeneratorn (TASK-031); kalkylen
# lägger en handgjord ö söder om Hemön.

NAMES.update({
    # Öppen mark för bostäder och runstenen; skogen lämnas åt skogshuggarna.
    "L4": (-2, 1), "L5": (0, 2), "L6": (-3, 0), "L7": (-1, -2), "L8": (0, -3), "L9": (0, 3),
    "L10": (2, 1), "L11": (-3, 2), "L12": (2, -3), "L13": (3, -3),
    "K3": (2, 0), "K4": (-2, 3),  # fiskevatten intill förråden
    "V1": ev1.CREST,  # Vårdkasen står på toppen, startöns enda högsta ruta (D-92)
    "N3": (10, 0), "N4": (9, 2),
    "Y0": (4, 8), "Y1": (5, 8), "Y2": (4, 9), "Y3": (3, 9), "Y4": (4, 7),
})
TILES[coord("N4")].richness = 1.5
add_island("Ytterön", {(4, 8): BEACH, (4, 7): GRASS, (3, 8): GRASS, (3, 9): FARMLAND,
                       (5, 7): FOREST, (5, 8): FOREST, (6, 7): FOREST},
           richness={(5, 7): 1.5, (5, 8): 1.5, (6, 7): 1.5, (4, 9): 2.0, (3, 9): 1.5})
ev1.EXPEDITIONS.append(("Ytterön", 400, 48))

# Runstenen: här ristas forskningen, och den bär världens historik (PRG-04).
# En bybo bemannar den; extraarbete på den snabbar på forskningen som på annan produktion.
SPECS["Runsten"] = Spec({"sten": 20}, 16, 0, 1, "land")

# Forskning kostar ingen kraft, bara varor och tid (beslut i epoch-1.md).
RESEARCH_COSTS = [{"plankor": 20, "sten": 10}, {"plankor": 20, "sten": 10},
                  {"plankor": 20, "tyg": 10}, {"plankor": 20, "tyg": 10},
                  {"plankor": 30, "tyg": 15}, {"plankor": 30, "tyg": 15}]
RESEARCH_HOURS = 48     # aktiva timmar per steg, tre dagar, ett steg åt gången
FOG_COST = 10           # Utforskning per ruta när dimman lyfts ruta för ruta (MAP-04)
FOG_TILES = 300         # dimrutor kring ögruppen i kalkylen
FOG_INNER = 150         # rutor över innerhavet som Vårdkasen ska synas över

# Vårdkasen byggs i tre etapper på toppen. Varje etapp har egna förutsättningar.
BEACON_VILLAGERS = 50
BEACON_POWER = 500      # Styrka per etapp; går att spara ihop under det fulla taket 600
BEACON = [
    ("Grunden", Spec({"sten": 80}, 160, BEACON_POWER, 0, "topp"),
     lambda w: w.linked("Nyön")),
    ("Stommen", Spec({"plankor": 80, "sten": 60}, 160, BEACON_POWER, 0, "topp"),
     lambda w: w.linked("Ytterön") and w.research_done >= 2),
    ("Elden", Spec({"tyg": 60, "plankor": 40}, 160, BEACON_POWER, 0, "topp"),
     lambda w: w.research_done >= 4 and w.fog_lifted >= FOG_INNER
     and w.villagers >= BEACON_VILLAGERS),
]

SPECS["Vårdkase"] = Spec({}, 0, 0, 0, "topp")  # byggnaden på kartan; etapperna betalas var för sig

# Handkraft: det som milstolpen kräver kan byggas utan Styrka. Byborna bygger då sten för
# sten på sin fritid: bygget tar ett fast antal dagar och tar ingen arbetskraft från kön.
HAND_DAYS = {"Brygga": 7, "Väveri": 7, "Vårdkasen": 21}

# --- Egenvård → krafter (docs/design/forces.md) ----------------------------------

STEP_CURVE = [(4000, 15), (8000, 7.5), (12000, 2.5), (20000, 1.25)]
BUILD_CUMULATIVE = [0, 100, 190, 270, 320, 345]


def exploration(steps: int) -> int:
    total, low = 0.0, 0
    for high, per_k in STEP_CURVE:
        total += max(0, min(steps, high) - low) / 1000 * per_k
        low = high
    return int(total)


def dawn_power(sleep_hours) -> int:
    if sleep_hours is None:
        return 40  # grundnivå
    return int(40 + 8 * min(sleep_hours, 7) + 2 * max(0, min(sleep_hours, 9) - 7))


def build_cumulative(n: int) -> int:
    return BUILD_CUMULATIVE[min(n, len(BUILD_CUMULATIVE) - 1)]


def build_power(passes: list, credits: list, day: int, rule: str) -> int:
    """Styrka för en dag.

    'nu': G(n + m) − G(n) med n = pass under de sex föregående dagarna (forces.md v0.1).
    'teleskop': G(pass under sju dagar) minus det som redan krediterats de sex
    föregående, bara på dygn med pass; varje rullande vecka ger då högst G(antal pass).
    """
    m = passes[day]
    prev = sum(passes[max(0, day - 6):day])
    if rule == "nu":
        return build_cumulative(prev + m) - build_cumulative(prev)
    if not m:
        return 0
    return max(0, build_cumulative(prev + m) - sum(credits[max(0, day - 6):day]))


# Vanor per veckodag (index 0–6): steg, pass och sömntimmar (None = saknad data).
PROFILES = {
    "hög": {"steps": [15000] * 7, "passes": [1, 1, 1, 1, 1, 1, 0], "sleep": [8.0] * 7},
    "normal": {"steps": [7500] * 7, "passes": [1, 0, 1, 0, 1, 0, 0], "sleep": [6.5] * 7},
    # Enbart manuell inmatning: 40 aktiva minuter fem dagar i veckan (D-41), tre pass,
    # sömn inskriven fem nätter av sju; resten lämnas tomt.
    "enbart manuell": {"steps": [4000] * 5 + [0, 0], "passes": [1, 0, 1, 0, 1, 0, 0],
                       "sleep": [7.0] * 5 + [None, None]},
    "låg": {"steps": [3000] * 7, "passes": [1, 0, 0, 0, 0, 0, 0], "sleep": [6.0] * 7},
    "utan träning": {"steps": [7500] * 7, "passes": [0] * 7, "sleep": [6.5] * 7},
    "ingen": {"steps": [0] * 7, "passes": [0] * 7, "sleep": [None] * 7},
}

# --- Epokens värld ---------------------------------------------------------------

STAFF_ORDER = ["Förråd", "Brygga", "Väveri", "Sågverk", "Runsten", "Fårgård", "Stenbrott",
               "Skogshuggare", "Åker", "Fiskebod"]
FOOD_STAFF = ["Förråd", "Åker", "Fiskebod"]
STAFF_FOOD_FIRST = FOOD_STAFF + [k for k in STAFF_ORDER if k not in FOOD_STAFF]
# Matlager, i dagar av basbehov, under vilket bemanningen sätter maten först. Tre dygn räcker
# för att hinna bemanna om fiskebodar och åkrar innan byborna går hungriga och tillväxten stannar.
FOOD_DAYS_KEPT = 3


class EpochWorld(World):
    def __init__(self, profile: str, rule: str, hand: bool):
        self.builders = 0  # lediga bybor som bemanningen lämnar till byggen (gratis planering)
        super().__init__()
        self.profile, self.rule, self.hand = profile, rule, hand
        self.passes, self.b_credits = [], []
        self.research_done = 0
        self.research_left = 0.0
        self.fog_lifted = 0
        self.beacon_stage = 0       # färdiga etapper
        self.beacon_day = None
        self.day = 0
        self.firsts = {}            # händelse → första dagen
        self.hand_projects = []     # (klart dag, etikett, när klart)
        self.decisions = {}         # vecka → beslut

    def staff(self):
        # Bemanningen är gratis planering (D-23): förädling och nya öar först, mat sist när
        # lagret räcker. Med byggplatser på rutor finns fler fiskebodar än byn kan bemanna.
        food_first = self.stock["fisk"] + self.stock["säd"] < FOOD_DAYS_KEPT * self.villagers
        order = STAFF_FOOD_FIRST if food_first else STAFF_ORDER
        for b in self.buildings:
            b.staffed = False
        for b in sorted(self.buildings, key=lambda b: order.index(b.kind) if b.kind in order else len(order)):
            need = SPECS[b.kind].staff if b.kind in SPECS else 0
            if not b.staffed and need and self.free_villagers() - need >= self.builders:
                b.staffed = True

    def free_villagers(self):
        return self.villagers - sum(SPECS[b.kind].staff for b in self.buildings
                                    if b.staffed and b.kind in SPECS)

    # -- krafter från vanor --
    def dawn(self, day, profile=None):
        self.day = day
        p, wd = PROFILES[self.profile], day % WEEK
        self.passes.append(p["passes"][wd])
        b = build_power(self.passes, self.b_credits, day, self.rule)
        self.b_credits.append(b)
        for f, amount in (("U", exploration(p["steps"][wd])), ("B", b), ("G", dawn_power(p["sleep"][wd]))):
            before = self.forces[f]
            self.forces[f] = ev1.credit(before, amount, f)
            self.credited[f] += self.forces[f] - before
        # Basbehov och tillväxt som i ekonomi v1.
        self.boosted = set()
        need = self.villagers
        for r in ev1.FOOD:
            take = min(self.stock[r], need)
            self.stock[r] -= take
            need -= take
        fed = need == 0
        comfort = fed and self.stock["tyg"] >= 2
        if comfort:
            self.stock["tyg"] -= 2
        self.villagers = min(self.housing(), self.villagers + (1 if fed else 0) + (1 if comfort else 0))
        self.staff()
        for p in [p for p in self.hand_projects if p[0] <= day]:
            self.hand_projects.remove(p)
            p[2]()

    # -- hjälpare --
    def note(self, what):
        self.firsts.setdefault(what, self.day)

    def decide(self, ok, what):
        if ok:
            self.decisions.setdefault(self.day // WEEK, []).append(what)
        return ok

    def linked(self, island):
        return island in self.remote_stock and self.has("Brygga", island) and self.has("Brygga", ev1.HOME)

    def b_rate(self):
        recent = self.b_credits[-WEEK:]
        return sum(recent) / len(recent) if recent else 0.0

    def use_hand(self, bp, hand_days, reserve):
        """Handkraft om det går fortare än att vänta in Styrkan."""
        if not self.hand or self.forces["B"] - bp >= reserve:
            return False
        rate = self.b_rate()
        return rate <= 0 or hand_days < (bp + reserve - self.forces["B"]) / rate

    def by_hand(self, label, cost, on_done):
        if not self.pay(cost, 0):
            return False
        self.hand_projects.append((self.day + HAND_DAYS[label.split("@")[0]], label, on_done))
        return True

    def pending(self, label):
        return super().pending(label) or any(p[1] == label for p in self.hand_projects)

    def occupied(self, tile):
        return super().occupied(tile) or any(p[1].endswith("@" + ev1.tag(coord(tile))) for p in self.hand_projects)

    def build_kind(self, kind, tile, reserve):
        """Bygg med Styrka om den räcker utöver det som sparas, annars kanske med handkraft."""
        spec = SPECS[kind]
        if not spec.build_power:
            return self.build(kind, tile)
        if self.forces["B"] - spec.build_power >= reserve:
            return self.decide(self.build(kind, tile), kind)
        if kind in HAND_DAYS and self.use_hand(spec.build_power, HAND_DAYS[kind], reserve):
            if not self.can_build(kind, tile):
                return False
            p = coord(tile)
            ok = self.by_hand(f"{kind}@{ev1.tag(p)}", spec.cost,
                              lambda: (self.buildings.append(Building(kind, p)), self.staff()))
            return self.decide(ok, f"{kind} med handkraft")
        return False

    # -- nya kommandon --
    def research(self):
        staffed = any(b.kind == "Runsten" and b.staffed for b in self.buildings)
        if not staffed or self.research_left or self.research_done >= len(RESEARCH_COSTS):
            return False
        if not self.pay(RESEARCH_COSTS[self.research_done], 0):
            return False
        self.research_left = RESEARCH_HOURS
        return True

    def lift_fog(self, tiles):
        n = min(tiles, FOG_TILES - self.fog_lifted, int(self.forces["U"] // FOG_COST))
        if n <= 0 or not self.spend("U", n * FOG_COST):
            return 0
        self.fog_lifted += n
        return n

    def next_stage(self):
        if self.beacon_stage >= len(BEACON) or self.pending("Vårdkasen"):
            return None
        return BEACON[self.beacon_stage]

    def build_stage(self):
        stage = self.next_stage()
        if stage is None or not stage[2](self):
            return False
        name, spec, _ = stage

        def done():
            self.beacon_stage += 1
            if self.beacon_stage == 1:
                self.buildings.append(Building("Vårdkase", coord("V1")))
        if self.forces["B"] >= spec.build_power:
            if not self.pay(spec.cost, spec.build_power):
                return False
            self.projects.append(Project("Vårdkasen", spec.work, done))
            return self.decide(True, f"Vårdkasen: {name}")
        if self.use_hand(spec.build_power, HAND_DAYS["Vårdkasen"], 0):
            return self.decide(self.by_hand("Vårdkasen", spec.cost, done), f"Vårdkasen: {name} med handkraft")
        return False

    def hour(self, active):
        super().hour(active)
        if active and self.research_left:
            boost = 1.5 if any(k == "Runsten" for k, _ in self.boosted) else 1.0
            self.research_left = max(0.0, self.research_left - boost)
            if self.research_left == 0:
                self.research_done += 1
                self.note(f"forskningssteg {self.research_done}")

# --- Strategi: samma genomtänkta plan för alla profiler ----------------------------

BUILDERS = 4  # bemanningsinställning: minst fyra bybor bygger alltid (gratis planering)


def reserve_for_beacon(w: EpochWorld) -> int:
    """Styrka som sparas till nästa etapp av Vårdkasen (D-35), om den ska byggas med kraft."""
    stage = w.next_stage()
    if stage is None or not w.linked("Nyön") or w.b_rate() * WEEK < 150:
        return 0
    return stage[1].build_power


def strategy(w: EpochWorld, day: int):
    w.builders = BUILDERS
    w.staff()
    reserve = reserve_for_beacon(w)
    # Den guidade turen (resa 1): extraarbete, kartläggning, skogshuggare, brygga.
    if day == 0:
        w.decide(w.workday("Fiskebod", "K1"), "Arbeta extra")
    w.decide(w.survey(), "Kartläggning")
    w.build("Skogshuggare", "F3")
    w.build_kind("Brygga", "H1", 0)
    # Grundkedjan på Hemön.
    w.build("Stenbrott", "M2")
    w.build_kind("Sågverk", "E2", 0)
    w.build("Förråd", "E1")
    w.decide(w.expedition(), "Expedition")
    # Sjövägar: bryggor på de nya öarna går före sparandet.
    w.build_kind("Brygga", "N9", 0)
    w.build_kind("Brygga", "Y0", 0)
    # Vårdkasen så fort nästa etapp går.
    w.build_stage()
    # Bostad: långhus när Styrkan räcker, annars stuga (D-43).
    if w.housing() - w.villagers < 3:
        for tile in ["L1", "L2", "L4", "L5", "L6", "L7", "L8", "L10", "L11", "L12", "L13", "N3", "Y4"]:
            if w.occupied(tile) or coord(tile) not in w.revealed:
                continue
            if not w.build_kind("Långhus", tile, reserve):
                w.build("Stuga", tile)
            break
    # Ull och tyg för snabbare tillväxt och för Elden.
    w.build("Fårgård", "N1")
    w.build("Stenbrott", "N2")
    w.build_kind("Väveri", "L3", reserve)
    # Mat för en växande by.
    for kind, tile in [("Fiskebod", "K2"), ("Åker", "O2"), ("Fiskebod", "N4"), ("Fiskebod", "K4"),
                       ("Fiskebod", "K3"), ("Skogshuggare", "Y1"), ("Fiskebod", "Y2"), ("Åker", "Y3")]:
        w.build(kind, tile)
    # Forskning när två öar är förbundna.
    if w.linked("Nyön"):
        w.build("Runsten", "L9")
    # Uppgraderingar med Styrka och plankor som blir över när nästa etapp är säkrad.
    stage = w.next_stage()
    plank_kept = stage[1].cost.get("plankor", 0) if stage else 0
    for kind, tile in [("Sågverk", "E2"), ("Stenbrott", "M2"), ("Fårgård", "N1"), ("Väveri", "L3")]:
        if (w.forces["B"] - ev1.UPGRADE.build_power >= reserve + 60
                and w.stock["plankor"] - ev1.UPGRADE.cost["plankor"] >= plank_kept):
            w.decide(w.upgrade(kind, tile), "Uppgradering")
    # Forskning är gratis att starta när varorna finns (planering, D-23); fyra steg räcker till Elden.
    if w.research_done < 4 or stage is None:
        w.decide(w.research(), "Forskningssteg")
    # Ork: dagens flaskhals, forskningen först när den pågår.
    for kind, tile in [("Runsten", "L9"), ("Sågverk", "E2"), ("Stenbrott", "M2"), ("Fårgård", "N1"),
                       ("Väveri", "L3")]:
        if kind == "Runsten" and not w.research_left:
            continue
        if w.forces["G"] >= ev1.DAWN_WORKDAY_COST:
            w.decide(w.workday(kind, tile), "Arbeta extra")
    if w.forces["G"] >= ev1.DAWN_RUSH_COST:
        w.decide(w.rush(), "Skynda på")
    # Utforskning som blir över: dimman ruta för ruta, men spara till nästa expedition.
    nxt = w.expedition_target
    keep_u = ev1.EXPEDITIONS[nxt][1] if nxt < len(ev1.EXPEDITIONS) and not w.expedition_left else 0
    if w.has("Brygga") and w.forces["U"] - keep_u >= FOG_COST:
        if w.lift_fog(int((w.forces["U"] - keep_u) // FOG_COST)):
            w.decide(True, "Lyft dimman")


# --- Körning ---------------------------------------------------------------------

def run(profile, rule="teleskop", hand=True):
    w = EpochWorld(profile, rule, hand)
    monotone = lambda: (w.villagers, len(w.buildings), sum(b.level for b in w.buildings),
                        len(w.revealed), w.research_done, w.fog_lifted, w.beacon_stage)
    for day in range(MAX_WEEKS * WEEK):
        w.dawn(day)
        strategy(w, day)
        for h in range(24):
            before = monotone()
            w.hour(active=h < ACTIVE_HOURS)
            after = monotone()
            assert all(a >= b for a, b in zip(after, before)), "förfall (INV-V1)"
            assert all(v >= 0 for v in w.stock.values()), "negativt lager (INV-R1)"
            assert all(v >= 0 for v in w.forces.values()), "negativt saldo (INV-F1)"
        for what, ok in [("brygga", w.has("Brygga", ev1.HOME)),
                         ("Nyön hittad", "Nyön" in w.remote_stock), ("sjöväg Nyön", w.linked("Nyön")),
                         ("runsten", w.has("Runsten")),
                         ("Ytterön hittad", "Ytterön" in w.remote_stock),
                         ("sjöväg Ytterön", w.linked("Ytterön")),
                         ("innerhavet", w.fog_lifted >= FOG_INNER),
                         (f"{BEACON_VILLAGERS} bybor", w.villagers >= BEACON_VILLAGERS),
                         ("grunden", w.beacon_stage >= 1), ("stommen", w.beacon_stage >= 2)]:
            if ok:
                w.note(what)
        if w.beacon_stage == len(BEACON):
            w.beacon_day = day
            w.note("elden")
            break
    return w


ORDER = ["hög", "normal", "enbart manuell", "låg", "utan träning", "ingen"]
MILESTONES = ["brygga", "Nyön hittad", "sjöväg Nyön", "runsten", "grunden", "Ytterön hittad",
              "sjöväg Ytterön", "forskningssteg 2", "stommen", "forskningssteg 4", "innerhavet",
              f"{BEACON_VILLAGERS} bybor", "elden"]


def week(day):
    return "–" if day is None else f"{day // WEEK + 1}"


def main():
    sys.stdout.reconfigure(encoding="utf-8")

    print("## Styrka per rullande vecka i jämvikt (vecka 10)\n")
    print("| Pass i veckan | Regel nu | Teleskop |")
    print("|---|---|---|")
    for n in range(1, 8):
        pattern = {2: [1, 0, 0, 1, 0, 0, 0], 3: [1, 0, 1, 0, 1, 0, 0]}.get(
            n, [1 if d < n else 0 for d in range(WEEK)])
        sums = []
        for rule in ("nu", "teleskop"):
            passes, credits = [], []
            for d in range(10 * WEEK):
                passes.append(pattern[d % WEEK])
                credits.append(build_power(passes, credits, d, rule))
            sums.append(sum(credits[-WEEK:]))
        print(f"| {n} | {sums[0]} | {sums[1]} |")

    print("\n## Krafter per vecka från vanorna (från vecka 2)\n")
    print("| Profil | Utforskning | Styrka, teleskop | Styrka, regel nu | Ork |")
    print("|---|---|---|---|---|")
    for name in ORDER:
        p = PROFILES[name]
        sums = []
        for rule in ("teleskop", "nu"):
            passes, credits = [], []
            for d in range(3 * WEEK):
                passes.append(p["passes"][d % WEEK])
                credits.append(build_power(passes, credits, d, rule))
            sums.append(sum(credits[-WEEK:]))
        u = sum(exploration(s) for s in p["steps"])
        g = sum(dawn_power(s) for s in p["sleep"])
        print(f"| {name} | {u} | {sums[0]} | {sums[1]} | {g} |")

    for rule, hand in (("teleskop", True), ("teleskop", False), ("nu", True)):
        print(f"\n## Vecka då spelaren når varje steg (Styrka: {rule}, "
              f"handkraft: {'ja' if hand else 'nej'})\n")
        print("| Profil | " + " | ".join(MILESTONES)
              + " | Dygn | Bybor | Byggnader | Dagsproduktion | Använt U/S/O | Sparat U/S/O |")
        print("|---|" + "---|" * (len(MILESTONES) + 6))
        for name in ORDER:
            w = run(name, rule, hand)
            cells = [week(w.firsts.get(m)) for m in MILESTONES]
            print(f"| {name} | " + " | ".join(cells)
                  + f" | {'–' if w.beacon_day is None else w.beacon_day + 1} | {w.villagers} "
                  f"| {len(w.buildings)} | {w.daily_output():.0f} "
                  f"| {w.spent['U']}/{w.spent['B']}/{w.spent['G']} "
                  f"| {w.forces['U']:.0f}/{w.forces['B']:.0f}/{w.forces['G']:.0f} |")

    print("\n## Beslut per vecka (teleskop, handkraft)\n")
    print("| Profil | Vecka | Antal | Slag |")
    print("|---|---|---|---|")
    for name in ORDER:
        w = run(name, "teleskop", True)
        last = (w.beacon_day if w.beacon_day is not None else 12 * WEEK - 1) // WEEK
        for i in range(min(last + 1, 12)):
            ds = w.decisions.get(i, [])
            kinds = ", ".join(sorted(set(ds))) if ds else "**inget**"
            print(f"| {name} | {i + 1} | {len(ds)} | {kinds} |")


if __name__ == "__main__":
    main()
