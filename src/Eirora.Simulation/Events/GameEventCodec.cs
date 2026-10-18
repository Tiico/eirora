using System;
using System.Collections.Generic;
using Eirora.Simulation.Economy;
using Eirora.Simulation.Forces;
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
                    writer.Line("villagers-moved-in").Long(e.UtcSeconds).Long(e.Count);
                    break;
                case ProjectCompleted e:
                    writer.Line("project-completed").Long(e.UtcSeconds).Enum(e.Kind).Enum(e.Building).Text(e.SiteId);
                    break;
                case SitesRevealed e:
                    writer.Line("sites-revealed").Long(e.UtcSeconds);
                    foreach (string siteId in e.SiteIds)
                    {
                        writer.Text(siteId);
                    }

                    break;
                case ExpeditionReturned e:
                    writer.Line("expedition-returned").Long(e.UtcSeconds);
                    foreach (string siteId in e.SiteIds)
                    {
                        writer.Text(siteId);
                    }

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
                return new VillagersMovedIn(reader.Long(), reader.Int());
            }

            if (reader.Next("project-completed"))
            {
                return new ProjectCompleted(reader.Long(), reader.Enum<ProjectKind>(), reader.Enum<BuildingKind>(), reader.Text());
            }

            if (reader.Next("sites-revealed"))
            {
                return new SitesRevealed(reader.Long(), SiteIds(reader));
            }

            if (reader.Next("expedition-returned"))
            {
                return new ExpeditionReturned(reader.Long(), SiteIds(reader));
            }

            throw new FormatException($"Expected an event, found '{reader.Peek ?? "the end"}'.");
        }

        private static List<string> SiteIds(StateReader reader)
        {
            var siteIds = new List<string>();
            while (reader.HasField)
            {
                siteIds.Add(reader.Text());
            }

            return siteIds;
        }
    }
}
