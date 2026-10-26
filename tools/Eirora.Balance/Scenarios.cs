namespace Eirora.Balance;

/// <summary>
/// A scenario (TASK-053; economy-v2.md, section 15): a map, a start and a self-care profile on which one strategy is
/// best. Every scenario starts where the guided tour leaves the world, founded as the map gives, and is played for four
/// weeks, so that the levels, the unlocking, the settlements and the carriers are all in the decisions.
/// </summary>
public sealed class Scenario
{
    /// <summary>Four weeks: the last step, seafaring, is reached in week 3–4 (economy-v2.md, section 10).</summary>
    public const int Days = 28;

    private Scenario(string id, string name, MapChoice map, SelfCareProfile profile, Strategy best)
    {
        Id = id;
        Name = name;
        Map = map;
        Profile = profile;
        Best = best;
    }

    /// <summary>
    /// Rik hemö: forest on four tiles next to the start and three mountains within two steps of it, so that Närområdet's
    /// cluster of woodcutter, quarry, storehouse and sawmill is best.
    /// </summary>
    public static Scenario RichHome { get; } = new("rich-home", "Rik hemö", MapChoice.Generated(174), SelfCareProfile.Normal, Strategy.Near);

    /// <summary>
    /// Rik grannö: only one mountain within two steps of the start, and an island for the expedition with rich pasture and
    /// a rich mountain; Havet, which founds a settlement there in the first week, is best.
    /// </summary>
    public static Scenario RichNeighbour { get; } = new("rich-neighbour", "Rik grannö", MapChoice.Generated(153), SelfCareProfile.Normal, Strategy.Sea);

    /// <summary>
    /// Prototypens karta: the handmade map; Bygden, which builds clothes and housing first, is best.
    /// </summary>
    public static Scenario Prototype { get; } = new("prototype", "Prototypens karta", MapChoice.Prototype, SelfCareProfile.Normal, Strategy.Village);

    /// <summary>
    /// Utan träning: a player who has deselected training (D-43), on a map whose island for the expedition has rich pasture
    /// and a rich mountain; Havet with the jetties and the weavery built by hand (D-65) is best.
    /// </summary>
    public static Scenario WithoutTraining { get; } = new("without-training", "Utan träning", MapChoice.Generated(31), SelfCareProfile.NoTraining, Strategy.NoTrainingByHand);

    public static IReadOnlyList<Scenario> All { get; } = [RichHome, RichNeighbour, Prototype, WithoutTraining];

    /// <summary>The thoughtful strategies the scenarios compare: each one best in a scenario, or a variant of one that is.</summary>
    public static IReadOnlyList<Strategy> Thoughtful { get; } = [Strategy.Near, Strategy.Sea, Strategy.Village, Strategy.NoTraining, Strategy.NoTrainingByHand];

    /// <summary>The thoughtful strategies and the naive one, which every thoughtful one must beat by D-34's margin.</summary>
    public static IReadOnlyList<Strategy> Compared { get; } = [.. Thoughtful, Strategy.Naive];

    /// <summary>The id the tool takes on its command line.</summary>
    public string Id { get; }

    /// <summary>The scenario's name in economy-v2.md.</summary>
    public string Name { get; }

    public MapChoice Map { get; }

    public SelfCareProfile Profile { get; }

    /// <summary>The strategy that gives the greatest development value after four weeks.</summary>
    public Strategy Best { get; }

    /// <summary>Every compared strategy on the scenario's map with <paramref name="profile"/>, or with its own profile.</summary>
    public IReadOnlyList<RunSpec> Runs(SelfCareProfile? profile = null) =>
        [.. Compared.Select(strategy => new RunSpec(Map, strategy, profile ?? Profile, Days))];
}
