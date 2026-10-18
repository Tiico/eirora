using System;
using System.Globalization;

namespace Eirora.Client.App
{
    /// <summary>
    /// The version of a build, derived from the number of commits so that every later build has a higher Android
    /// version code and installs over the one before (TASK-014). Local builds and CI use the same rule.
    /// </summary>
    public readonly struct BuildVersion
    {
        private BuildVersion(string name, int code)
        {
            Name = name;
            Code = code;
        }

        /// <summary>The version people see, such as 0.0.142.</summary>
        public string Name { get; }

        /// <summary>The Android version code, which must grow with every update.</summary>
        public int Code { get; }

        public static BuildVersion FromCommitCount(int commits) => commits > 0
            ? new BuildVersion("0.0." + commits.ToString(CultureInfo.InvariantCulture), commits)
            : throw new ArgumentOutOfRangeException(nameof(commits), commits, "A build has at least one commit.");
    }
}
