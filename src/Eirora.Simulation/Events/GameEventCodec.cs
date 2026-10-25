using System;
using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
using Eirora.Simulation.Map;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation.Events
{
    /// <summary>
    /// Events as state records, so that a used idempotency key keeps its outcome in a snapshot (INV-D6).
    /// Every kind of event has a record, whether or not a command can cause it today.
    /// </summary>
    internal static class GameEventCodec
    {
        public static void Write(StateWriter writer, GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case DuskFell e:
                    writer.Line("dusk-fell").Long(e.UtcSeconds);
                    break;
                case DawnBroke e:
                    writer.Line("dawn-broke").Long(e.UtcSeconds).Long(e.GameDay);
                    break;
                case ForceCredited e:
                    writer.Line("force-credited").Long(e.UtcSeconds).Enum(e.Force).Long(e.Amount);
                    break;
                case ForceWithdrawn e:
                    writer.Line("force-withdrawn").Long(e.UtcSeconds).Enum(e.Force).Long(e.Amount);
                    break;
                case VillagersMovedIn e:
                    writer.Line("villagers-moved-in").Long(e.UtcSeconds).Long(e.Count).Text(e.Island);
                    break;
                case VillagerMoved e:
                    writer.Line("villager-moved").Long(e.UtcSeconds).Text(e.From).Text(e.To);
                    break;
                case SettlementFounded e:
                    writer.Line("settlement-founded").Long(e.UtcSeconds).Text(e.Island);
                    break;
                case ProjectCompleted e:
                    // A road lies on a tile of its own and names no building.
                    var line = writer.Line("project-completed").Long(e.UtcSeconds).Enum(e.Kind);
                    if (e.Kind != ProjectKind.Road && e.Building is BuildingKind kind)
                    {
                        line.Enum(kind);
                    }

                    line.Long(e.Tile.Q).Long(e.Tile.R);
                    break;
                case BuildingDemolished e:
                    writer.Line("building-demolished").Long(e.UtcSeconds).Enum(e.Building).Long(e.Tile.Q).Long(e.Tile.R);
                    break;
                case TilesRevealed e:
                    writer.Line("tiles-revealed").Long(e.UtcSeconds);
                    foreach (var tile in e.Tiles)
                    {
                        writer.Long(tile.Q).Long(tile.R);
                    }

                    break;
                case FogLifted e:
                    writer.Line("fog-lifted").Long(e.UtcSeconds).Long(e.Q).Long(e.R);
                    break;
                case ExpeditionReturned e:
                    writer.Line("expedition-returned").Long(e.UtcSeconds).Text(e.Island);
                    break;
                default:
                    throw new ArgumentException($"{gameEvent.GetType().Name} has no state record.", nameof(gameEvent));
            }
        }

        public static GameEvent Read(StateReader reader)
        {
            if (reader.Next("dusk-fell"))
            {
                return new DuskFell(reader.Long());
            }

            if (reader.Next("dawn-broke"))
            {
                return new DawnBroke(reader.Long(), reader.Int());
            }

            if (reader.Next("force-credited"))
            {
                return new ForceCredited(reader.Long(), reader.Enum<Force>(), reader.Int());
            }

            if (reader.Next("force-withdrawn"))
            {
                return new ForceWithdrawn(reader.Long(), reader.Enum<Force>(), reader.Int());
            }

            if (reader.Next("villagers-moved-in"))
            {
                return new VillagersMovedIn(reader.Long(), reader.Int(), reader.Text());
            }

            if (reader.Next("villager-moved"))
            {
                return new VillagerMoved(reader.Long(), reader.Text(), reader.Text());
            }

            if (reader.Next("settlement-founded"))
            {
                return new SettlementFounded(reader.Long(), reader.Text());
            }

            if (reader.Next("project-completed"))
            {
                long utc = reader.Long();
                var kind = reader.Enum<ProjectKind>();
                BuildingKind? building = kind == ProjectKind.Road ? null : reader.Enum<BuildingKind>();
                return new ProjectCompleted(utc, kind, building, new Hex(reader.Int(), reader.Int()));
            }

            if (reader.Next("building-demolished"))
            {
                return new BuildingDemolished(reader.Long(), reader.Enum<BuildingKind>(), new Hex(reader.Int(), reader.Int()));
            }

            if (reader.Next("tiles-revealed"))
            {
                return new TilesRevealed(reader.Long(), Tiles(reader));
            }

            if (reader.Next("fog-lifted"))
            {
                return new FogLifted(reader.Long(), reader.Int(), reader.Int());
            }

            if (reader.Next("expedition-returned"))
            {
                return new ExpeditionReturned(reader.Long(), reader.Text());
            }

            throw new FormatException($"Expected an event, found '{reader.Peek ?? "the end"}'.");
        }

        private static List<Hex> Tiles(StateReader reader)
        {
            var tiles = new List<Hex>();
            while (reader.HasField)
            {
                tiles.Add(new Hex(reader.Int(), reader.Int()));
            }

            return tiles;
        }
    }
}
