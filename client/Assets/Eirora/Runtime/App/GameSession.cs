using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Eirora.Client.Health;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>
    /// The running game: the world's journal, catching up with the clock, and saving after every command.
    /// The journal is the save (INV-D5); loading replays it.
    /// </summary>
    public sealed class GameSession
    {
        private readonly IClock _clock;
        private readonly string? _savePath;
        private readonly List<GameEvent> _sinceDawn = new();
        private Journal? _journal;
        private bool _syncing;

        public GameSession(IClock clock, string? savePath)
        {
            _clock = clock;
            _savePath = savePath;
        }

        /// <summary>Raised whenever the world has changed and the view should follow.</summary>
        public event Action? Changed;

        public World? World => _journal?.World;

        /// <summary>What has happened since the latest dawn, for the daily report (UX-02).</summary>
        public IReadOnlyList<GameEvent> SinceDawn => _sinceDawn;

        public static string DefaultSavePath => Path.Combine(Application.persistentDataPath, "world.journal");

        /// <summary>Loads the saved world if there is one; returns false on first launch.</summary>
        public bool TryLoad()
        {
            LoadProblem = null;
            if (_savePath is null || !File.Exists(_savePath))
            {
                return false;
            }

            try
            {
                _journal = Journal.Load(File.ReadAllText(_savePath));
            }
            catch (FormatException problem)
            {
                // A journal this build cannot replay, for example from other rules, is never overwritten (TASK-014, ADR-0006).
                LoadProblem = problem.Message;
                return false;
            }

            CatchUp();
            return true;
        }

        /// <summary>Why the saved world could not be loaded, or null when nothing went wrong.</summary>
        public string? LoadProblem { get; private set; }

        /// <summary>
        /// Moves a save that could not be loaded aside under a dated name, so a new world can begin without losing it.
        /// </summary>
        public string? SetAsideSave()
        {
            if (_savePath is null || !File.Exists(_savePath))
            {
                return null;
            }

            string aside = $"{_savePath}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.unreadable";
            File.Move(_savePath, aside);
            LoadProblem = null;
            return aside;
        }

        /// <summary>Creates a new world with the chosen rest period (D-50).</summary>
        public void Begin(RestSchedule restSchedule)
        {
            _journal = Journal.Start(_clock.Now(), restSchedule);
            Save();
            Changed?.Invoke();
        }

        /// <summary>Lets the world catch up with the clock.</summary>
        public void CatchUp()
        {
            if (_journal is null)
            {
                return;
            }

            bool changed = false;
            foreach (var reading in _clock.ReadingsSince(_journal.World.Now.UtcSeconds))
            {
                foreach (var gameEvent in _journal.AdvanceTo(reading))
                {
                    Remember(gameEvent);
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Reads the health store and records what changed for the last seven game days (D-70). Does nothing without a
        /// world, a store or access; a read already under way is not started twice.
        /// </summary>
        public async Task SyncHealthAsync(HealthSync? sync)
        {
            if (sync is null || _journal is null || _syncing)
            {
                return;
            }

            _syncing = true;
            try
            {
                CatchUp();
                var commands = await sync.CommandsAsync(_journal.World);
                if (commands.Count == 0)
                {
                    return;
                }

                foreach (var command in commands)
                {
                    foreach (var gameEvent in _journal.Apply(Guid.NewGuid().ToString("N"), command).Events)
                    {
                        Remember(gameEvent);
                    }
                }

                Save();
                Changed?.Invoke();
            }
            finally
            {
                _syncing = false;
            }
        }

        public CommandResult Apply(GameCommand command)
        {
            if (_journal is null)
            {
                throw new InvalidOperationException("There is no world yet.");
            }

            CatchUp();
            var result = _journal.Apply(Guid.NewGuid().ToString("N"), command);
            foreach (var gameEvent in result.Events)
            {
                Remember(gameEvent);
            }

            Save();
            Changed?.Invoke();
            return result;
        }

        public void Save()
        {
            if (_journal is null || _savePath is null)
            {
                return;
            }

            // Write beside the save and swap, so a crash never leaves half a journal.
            string temporary = _savePath + ".tmp";
            File.WriteAllText(temporary, _journal.ToText());
            if (File.Exists(_savePath))
            {
                File.Replace(temporary, _savePath, null);
            }
            else
            {
                File.Move(temporary, _savePath);
            }
        }

        private void Remember(GameEvent gameEvent)
        {
            if (gameEvent is DawnBroke)
            {
                _sinceDawn.Clear();
            }

            _sinceDawn.Add(gameEvent);
        }
    }
}
