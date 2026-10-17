using System.Collections.Generic;
using Eirora.Simulation.Events;

namespace Eirora.Simulation.Commands
{
    /// <summary>The outcome of a command and the events it caused.</summary>
    public sealed class CommandResult
    {
        internal CommandResult(Rejection rejection, IReadOnlyList<GameEvent> events)
        {
            Rejection = rejection;
            Events = events;
        }

        public bool Accepted => Rejection == Rejection.None;

        public Rejection Rejection { get; }

        public IReadOnlyList<GameEvent> Events { get; }
    }
}
