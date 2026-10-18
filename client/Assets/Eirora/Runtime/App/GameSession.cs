using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Eirora.Client.Health;
using Eirora.Simulation;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Time;
using UnityEngine;

namespace Eirora.Client.App
{
    /// <summary>Why a saved world could not be loaded; it is kept either way.</summary>
    public enum SaveProblem
    {
        /// <summary>The save and its backup are damaged or incomplete.</summary>
        Damaged,

        /// <summary>A newer version of the app wrote the save; updating the app will read it.</summary>
        FromNewerVersion,
    }

    /// <summary>
    /// The running game: the world's save, catching up with the clock, and saving after every command (ADR-0006).
    /// </summary>
    /// <remarks>
    /// Each save is written beside the old one and swapped in, keeping the previous one as a backup, so a crash or an
    /// empty battery mid-write never costs more than the latest command (NFR-01). A save that cannot be read is never
    /// overwritten. A prototype journal from M1 is taken over once and kept.
    /// </remarks>
    public sealed class GameSession
    {
        public const string SaveFile = "world.save";
        public const string PrototypeJournalFile = "world.journal";

        private readonly IClock _clock;
        private readonly string? _savePath;
        private readonly List<GameEvent> _sinceDawn = new();
        private readonly List<string> _unreadable = new();
        private Save? _save;
        private bool _syncing;

        /// <param name="savePath">Where the save lives, or null to keep the world in memory only.</param>
        public GameSession(IClock clock, string? savePath)
        {
            _clock = clock;
            _savePath = savePath;
        }

        /// <summary>Raised whenever the world has changed and the view should follow.</summary>
        public event Action? Changed;

        public World? World => _save?.World;

        /// <summary>What has happened since the latest dawn, for the daily report (UX-02).</summary>
        public IReadOnlyList<GameEvent> SinceDawn => _sinceDawn;

        public static string DefaultSavePath => Path.Combine(Application.persistentDataPath, SaveFile);

        /// <summary>Why the saved world could not be loaded, or null when nothing went wrong.</summary>
        public SaveProblem? LoadProblem { get; private set; }

        private string? BackupPath => _savePath is null ? null : _savePath + ".bak";

        private string? PrototypeJournalPath =>
            _savePath is null ? null : Path.Combine(Path.GetDirectoryName(_savePath) ?? string.Empty, PrototypeJournalFile);

        /// <summary>Loads the saved world if there is one; returns false on first launch or when it cannot be read.</summary>
        public bool TryLoad()
        {
            LoadProblem = null;
            _unreadable.Clear();
            if (_savePath is null)
            {
                return false;
            }

            _save = LoadSaveOrBackup();
            bool takingOver = false;
            if (_save is null && LoadProblem != SaveProblem.FromNewerVersion && File.Exists(PrototypeJournalPath))
            {
                _save = TakeOverPrototypeJournal(PrototypeJournalPath!);
                takingOver = _save is not null;
            }

            if (_save is null)
            {
                return false;
            }

            // What could not be read is kept for troubleshooting, out of the way of the next save.
            SetAside(_unreadable, "damaged");
            LoadProblem = null;
            if (_save.ChangedOnLoad)
            {
                Save();
            }

            if (takingOver)
            {
                File.Move(PrototypeJournalPath!, PrototypeJournalPath + ".taken-over");
            }

            CatchUp();
            return true;
        }

        /// <summary>
        /// Moves what could not be loaded aside under dated names, so a new world can begin without losing or
        /// overwriting it; returns where it went.
        /// </summary>
        public IReadOnlyList<string> SetAsideSave()
        {
            var aside = SetAside(_unreadable, "unreadable");
            LoadProblem = null;
            return aside;
        }

        /// <summary>Creates a new world with the chosen rest period (D-50).</summary>
        public void Begin(RestSchedule restSchedule)
        {
            _save = Simulation.Saving.Save.Begin(_clock.Now(), restSchedule);
            Save();
            Changed?.Invoke();
        }

        /// <summary>Lets the world catch up with the clock.</summary>
        public void CatchUp()
        {
            if (_save is null)
            {
                return;
            }

            bool changed = false;
            foreach (var reading in _clock.ReadingsSince(_save.World.Now.UtcSeconds))
            {
                foreach (var gameEvent in _save.AdvanceTo(reading))
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
            if (sync is null || _save is null || _syncing)
            {
                return;
            }

            _syncing = true;
            try
            {
                CatchUp();
                var commands = await sync.CommandsAsync(_save.World);
                if (commands.Count == 0)
                {
                    return;
                }

                foreach (var command in commands)
                {
                    foreach (var gameEvent in _save.Apply(Guid.NewGuid().ToString("N"), command).Events)
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
            if (_save is null)
            {
                throw new InvalidOperationException("There is no world yet.");
            }

            CatchUp();
            var result = _save.Apply(Guid.NewGuid().ToString("N"), command);
            foreach (var gameEvent in result.Events)
            {
                Remember(gameEvent);
            }

            Save();
            Changed?.Invoke();
            return result;
        }

        /// <summary>Writes the save beside the old one and swaps it in, keeping the old one as the backup.</summary>
        public void Save()
        {
            if (_save is null || _savePath is null)
            {
                return;
            }

            string temporary = _savePath + ".tmp";
            File.WriteAllBytes(temporary, _save.ToBytes());
            if (File.Exists(_savePath))
            {
                File.Replace(temporary, _savePath, BackupPath);
            }
            else
            {
                File.Move(temporary, _savePath);
            }
        }

        /// <summary>
        /// Loads the save, or the backup when the save is damaged or missing: on the phone a swap cut off between its two
        /// renames leaves only the backup. A save from a newer version stops the search, since an older backup would
        /// later overwrite it. Anything that cannot be read is remembered.
        /// </summary>
        private Save? LoadSaveOrBackup()
        {
            foreach (string path in new[] { _savePath!, BackupPath! })
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    return Simulation.Saving.Save.Load(File.ReadAllBytes(path));
                }
                catch (SavedByNewerVersionException)
                {
                    _unreadable.Add(path);
                    LoadProblem = SaveProblem.FromNewerVersion;
                    return null;
                }
                catch (FormatException)
                {
                    _unreadable.Add(path);
                    LoadProblem = SaveProblem.Damaged;
                }
            }

            return null;
        }

        /// <summary>
        /// Replays a prototype journal from M1 (ADR-0006). It is renamed only once the save it became is written, so a
        /// take-over cut off midway is done again.
        /// </summary>
        private Save? TakeOverPrototypeJournal(string path)
        {
            try
            {
                return Simulation.Saving.Save.FromPrototypeJournal(File.ReadAllText(path));
            }
            catch (FormatException)
            {
                _unreadable.Add(path);
                LoadProblem = SaveProblem.Damaged;
                return null;
            }
        }

        private static List<string> SetAside(List<string> paths, string reason)
        {
            var aside = new List<string>();
            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    string target = $"{path}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.{reason}";
                    File.Move(path, target);
                    aside.Add(target);
                }
            }

            paths.Clear();
            return aside;
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
