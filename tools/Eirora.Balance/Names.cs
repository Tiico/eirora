using Eirora.Simulation.Economy;

namespace Eirora.Balance;

/// <summary>The glossary's Swedish names, for the report.</summary>
internal static class Names
{
    public static string Of(BuildingKind kind) => kind switch
    {
        BuildingKind.Longhouse => "långhus",
        BuildingKind.Cottage => "stuga",
        BuildingKind.Storehouse => "förråd",
        BuildingKind.FishingHut => "fiskebod",
        BuildingKind.Field => "åker",
        BuildingKind.Woodcutter => "skogshuggare",
        BuildingKind.SheepFarm => "fårgård",
        BuildingKind.Quarry => "stenbrott",
        BuildingKind.Sawmill => "sågverk",
        BuildingKind.Weavery => "väveri",
        BuildingKind.Jetty => "brygga",
        BuildingKind.Lookout => "utkikstorn",
        BuildingKind.Farmstead => "bondgård",
        BuildingKind.HuntingLodge => "jaktstuga",
        BuildingKind.Tannery => "garveri",
        BuildingKind.CraftsmensHouse => "hantverkargård",
        BuildingKind.Apiary => "bigård",
        BuildingKind.Brewery => "bryggeri",
        BuildingKind.Meadery => "mjöderi",
        BuildingKind.TarKiln => "tjärdal",
        BuildingKind.Shipyard => "varv",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A building without a name in the glossary."),
    };

    public static string Of(Where where) => where switch
    {
        Where.Home => "hemön",
        Where.ExpeditionIsland => "expeditionens ö",
        _ => "expeditionens ö annars hemön",
    };
}
