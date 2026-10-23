namespace Eirora.Simulation.Economy
{
    /// <summary>
    /// The rules of transport on land (economy-v2.md, section 7; D-107, D-109–D-112): the carriers of the storehouses and
    /// jetties, the outboxes they empty and the roads that speed them. Times are millionths of an hour (INV-D3).
    /// </summary>
    public static class Logistics
    {
        /// <summary>The most carriers a storehouse or a jetty takes (D-109).</summary>
        public const int MostCarriers = 4;

        /// <summary>Whole units of its product a building's outbox holds; when it is full the building stops (INV-R2).</summary>
        public const int OutboxUnits = 8;

        /// <summary>Whole units of each raw material the carriers keep at the processing.</summary>
        public const int InputUnits = 4;

        /// <summary>Whole units a carrier takes on each trip there and back.</summary>
        public const int CarrierLoad = 4;

        /// <summary>A step over land, in millionths of an hour: 15 minutes.</summary>
        public const long StepTime = 250_000;

        /// <summary>A step onto a tile with a road: 7.5 minutes (D-112).</summary>
        public const long RoadStepTime = 125_000;

        /// <summary>What a road costs per tile in stone, and no force (D-112).</summary>
        public const int RoadStone = 2;

        /// <summary>The hours of work a road takes per tile (D-112).</summary>
        public const int RoadHours = 4;

        /// <summary>Whether a kind of building is a depot: a storehouse or a jetty, which has carriers and stores goods.</summary>
        public static bool IsDepot(BuildingKind kind) => kind is BuildingKind.Storehouse or BuildingKind.Jetty;
    }
}
