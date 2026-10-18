namespace Eirora.Client.Look
{
    /// <summary>Nature drawn on a hex, apart from the ground itself.</summary>
    /// <remarks>Looks store the number, so new kinds go last and none is ever removed or reordered.</remarks>
    public enum NatureKind
    {
        Forest,
        Farmland,
        Mountain,
    }
}
