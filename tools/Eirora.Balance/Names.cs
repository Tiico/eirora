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
        BuildingKind.Woodcutter => "huggarkoja",
        BuildingKind.SheepFarm => "fårgård",
        BuildingKind.Quarry => "stenbrott",
        BuildingKind.Sawmill => "sågverk",
        BuildingKind.Weavery => "väveri",
        BuildingKind.Jetty => "brygga",
        _ => "utkiksvarde",
    };

    public static string Of(Where where) => where switch
    {
        Where.Home => "hemön",
        Where.ExpeditionIsland => "expeditionens ö",
        _ => "expeditionens ö annars hemön",
    };
}
