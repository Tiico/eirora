using System;
using Eirora.Client.App;
using Eirora.Client.UI;
using Eirora.Simulation;
using NUnit.Framework;

namespace Eirora.Client.Tests
{
    /// <summary>Every new build installs over the one before, and says which build and rules it runs (TASK-014).</summary>
    public sealed class BuildVersionTests
    {
        [Test]
        public void ALaterCommitGivesAHigherVersionCode()
        {
            var earlier = BuildVersion.FromCommitCount(141);
            var later = BuildVersion.FromCommitCount(142);

            Assert.That(later.Code, Is.GreaterThan(earlier.Code));
            Assert.That(later.Name, Is.EqualTo("0.0.142"));
        }

        [Test]
        public void ABuildNeedsHistory() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildVersion.FromCommitCount(0));

        [Test]
        public void TheBuildLabelNamesTheVersionAndTheRules() =>
            Assert.That(Texts.Build("0.0.142"), Is.EqualTo($"Version 0.0.142 · regelversion {RulesVersion.Current}"));
    }
}
