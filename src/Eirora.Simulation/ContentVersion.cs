namespace Eirora.Simulation
{
    /// <summary>
    /// The version of the game's content: buildings, recipes, resources, finds and epochs (ADR-0006).
    /// Bumped when content is added; changing how existing content works bumps <see cref="RulesVersion"/> instead.
    /// </summary>
    public static class ContentVersion
    {
        public const int Current = 3;
    }
}
