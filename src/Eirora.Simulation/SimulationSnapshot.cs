using System;
using System.Collections.Generic;
using System.Text;
using Eirora.Simulation.Map;
using Eirora.Simulation.Saving;
using Eirora.Simulation.Serialization;

namespace Eirora.Simulation
{
    /// <summary>
    /// A world's whole state at one moment, labelled with its rules and content versions (GLOSSARY: Snapshot, ADR-0006).
    /// Restoring it and continuing with the inputs that came after gives the same world as never stopping (INV-D5).
    /// </summary>
    /// <remarks>
    /// The bytes are UTF-8 text: a header, then the world's fingerprint, which is its canonical state. The header
    /// names the format, rules and content versions, the simulation time and a SHA-256 hash of the state, so a changed
    /// byte is caught. Restoring writes the restored world again and compares, so the snapshot holds exactly the state
    /// the fingerprint covers. The header can be read without restoring, so a save can decide to migrate first.
    /// </remarks>
    public sealed class SimulationSnapshot
    {
        /// <summary>The container format. Bumped when the header, encoding or compression change.</summary>
        public const int FormatVersion = 1;

        private const string Magic = "eirora-snapshot";
        private const int HeaderLines = 5;

        private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private readonly string _state;

        private SimulationSnapshot(int format, int rules, int content, long utcSeconds, string stateHash, string state)
        {
            Format = format;
            Rules = rules;
            Content = content;
            UtcSeconds = utcSeconds;
            StateHash = stateHash;
            _state = state;
        }

        public int Format { get; }

        /// <summary>The rules version the state was simulated under; the state's shape follows it.</summary>
        public int Rules { get; }

        public int Content { get; }

        /// <summary>The simulation time of the state.</summary>
        public long UtcSeconds { get; }

        /// <summary>The SHA-256 hash of the state, in lower-case hex.</summary>
        public string StateHash { get; }

        public static SimulationSnapshot Take(World world)
        {
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            return Of(world, world.Fingerprint());
        }

        /// <summary>
        /// A snapshot of the world's state without what is kept per game day (ADR-0008, D-100); it is restored with
        /// <see cref="RestoreWorld"/>, and its <see cref="StateHash"/> is what the contributions that complete it name.
        /// </summary>
        internal static SimulationSnapshot TakeWorld(World world) => Of(world, world.WorldState());

        /// <summary>Reads a snapshot and checks its hash, whatever its rules and content versions.</summary>
        /// <exception cref="FormatException">The bytes are not an intact snapshot of this format.</exception>
        public static SimulationSnapshot FromBytes(byte[] bytes)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            string text;
            try
            {
                text = s_utf8.GetString(bytes);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("A snapshot is UTF-8 text.", e);
            }

            int end = 0;
            for (int line = 0; line < HeaderLines; line++)
            {
                int lineBreak = text.IndexOf('\n', end);
                end = lineBreak >= 0 ? lineBreak + 1 : throw new FormatException("A snapshot starts with a header.");
            }

            var header = new StateReader(text.Substring(0, end));
            header.Expect(Magic);
            int format = header.Int();
            if (format != FormatVersion)
            {
                throw format > FormatVersion
                    ? new SavedByNewerVersionException($"The snapshot has format {format}; this build reads format {FormatVersion}.")
                    : new FormatException($"The snapshot has format {format}; this build reads format {FormatVersion}.");
            }

            header.Expect("rules");
            int rules = header.Int();
            header.Expect("content");
            int content = header.Int();
            header.Expect("time");
            long utcSeconds = header.Long();
            header.Expect("state-sha256");
            string hash = header.Text();
            header.End();

            string state = text.Substring(end);
            if (!string.Equals(hash, Hash(state), StringComparison.Ordinal))
            {
                throw new FormatException("The snapshot's state does not match its hash.");
            }

            return new SimulationSnapshot(format, rules, content, utcSeconds, hash, state);
        }

        public byte[] ToBytes()
        {
            var header = new StateWriter();
            header.Line(Magic).Long(Format);
            header.Line("rules").Long(Rules);
            header.Line("content").Long(Content);
            header.Line("time").Long(UtcSeconds);
            header.Line("state-sha256").Text(StateHash);
            return s_utf8.GetBytes(header.ToString() + _state);
        }

        /// <summary>
        /// Whether this build cannot read the snapshot because a newer app wrote it: a newer format, rules or content.
        /// Such a snapshot is never overwritten; the player is asked to update (ADR-0006).
        /// </summary>
        public bool IsFromNewerVersion =>
            Format > FormatVersion || Rules > RulesVersion.Current || Content > ContentVersion.Current;

        /// <summary>
        /// Rebuilds the world on its map under the current rules and content, migrating the state forward from an older
        /// rules version (ADR-0006). The simulation time stays the same.
        /// </summary>
        /// <exception cref="FormatException">
        /// The snapshot is from a newer version, a migration is missing, it belongs to another map, or it does not
        /// restore to the state it holds.
        /// </exception>
        public World Restore(WorldMap map) => Restore(map, Migrations.All);

        internal World Restore(WorldMap map, IReadOnlyList<StateMigration> migrations) => Restore(map, _state, withContributions: true, migrations);

        /// <summary>
        /// Rebuilds the world from a snapshot made by <see cref="TakeWorld"/>, completed with the contribution state that
        /// followed it in the fingerprint; without it, the world goes on with its game days so far closed (D-101).
        /// </summary>
        internal World RestoreWorld(WorldMap map, string? contributions, IReadOnlyList<StateMigration> migrations)
        {
            // Every record ends its line, so the two parts join as the fingerprint writes them.
            return contributions is null
                ? Restore(map, _state, withContributions: false, migrations)
                : Restore(map, _state + contributions, withContributions: true, migrations);
        }

        private static SimulationSnapshot Of(World world, string state) =>
            new(FormatVersion, RulesVersion.Current, ContentVersion.Current, world.Now.UtcSeconds, Hash(state), state);

        private World Restore(WorldMap map, string state, bool withContributions, IReadOnlyList<StateMigration> migrations)
        {
            if (map is null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (IsFromNewerVersion)
            {
                throw new SavedByNewerVersionException(
                    $"The snapshot has rules version {Rules} and content version {Content}; this build has {RulesVersion.Current} and {ContentVersion.Current}.");
            }

            state = Migrations.ToCurrent(state, Rules, migrations);
            World world;
            try
            {
                var reader = new StateReader(state);
                world = World.Read(reader, map, withContributions);
                reader.End();
            }
            catch (ArgumentException e)
            {
                throw new FormatException("The snapshot holds a state the rules do not allow.", e);
            }

            string restored = withContributions ? world.Fingerprint() : world.WorldState();
            if (!string.Equals(restored, state, StringComparison.Ordinal) || world.Now.UtcSeconds != UtcSeconds)
            {
                throw new FormatException("The snapshot does not restore to the state it holds.");
            }

            return world;
        }

        private static string Hash(string state) => Sha256.Of(state);
    }
}
