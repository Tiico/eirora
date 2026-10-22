using Eirora.Simulation.Map;

namespace Eirora.Simulation.Tests;

/// <summary>
/// Tiles of the prototype's map by the names the economy model gives them (economy_v1.py, <c>NAMES</c>), the names of
/// the prototype's sites before every land tile became a building site (TASK-037), so that tests read like the design.
/// </summary>
internal static class Tiles
{
    /// <summary>The start village's storehouse.</summary>
    public static readonly Hex S = new(0, 0);

    public static readonly Hex L1 = new(-1, 0);
    public static readonly Hex L2 = new(0, -1);
    public static readonly Hex L3 = new(-1, 1);
    public static readonly Hex E1 = new(2, -1);
    public static readonly Hex E2 = new(2, -2);

    /// <summary>The start fishing hut's water.</summary>
    public static readonly Hex K1 = new(1, 0);

    /// <summary>Rich fishing water, found by the second survey.</summary>
    public static readonly Hex K2 = new(-3, 3);

    /// <summary>The beach south-east of the village, a harbour spot.</summary>
    public static readonly Hex H1 = new(1, 1);

    /// <summary>The start field.</summary>
    public static readonly Hex O1 = new(0, 1);

    /// <summary>Farmland found by the second survey.</summary>
    public static readonly Hex O2 = new(-1, 2);

    /// <summary>The start woodcutter's forest tile.</summary>
    public static readonly Hex F1 = new(1, -1);

    /// <summary>The forest east of the village.</summary>
    public static readonly Hex F2 = new(3, -2);

    /// <summary>The old-growth forest's rich tile, found by the first survey.</summary>
    public static readonly Hex F3 = new(-2, 0);

    /// <summary>Hemön's crest, the western mountain (D-92).</summary>
    public static readonly Hex M1 = new(-4, 0);

    /// <summary>The stone vein, found by the first survey.</summary>
    public static readonly Hex M2 = new(3, -1);

    /// <summary>The plain mountain beside the crest (D-95).</summary>
    public static readonly Hex M3 = new(-3, -1);

    public static readonly Hex P1 = new(-2, -1);

    /// <summary>Nyön's beach.</summary>
    public static readonly Hex N0 = new(10, 0);

    /// <summary>Nyön's rich pasture.</summary>
    public static readonly Hex N1 = new(10, 1);

    /// <summary>Nyön's mountain.</summary>
    public static readonly Hex N2 = new(11, 0);

    /// <summary>Nyön's harbour spot next to the pasture, where the model's jetty stands.</summary>
    public static readonly Hex N9 = new(9, 1);
}
