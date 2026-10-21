using System.Globalization;
using System.Reflection;
using Eirora.Simulation.Commands;
using Eirora.Simulation.Events;
using Eirora.Simulation.Map;

namespace Eirora.Simulation.Tests;

/// <summary>Text forms of results and events, so that tests can compare them by value.</summary>
internal static class Describe
{
    public static string Result(CommandResult result) =>
        $"{result.Rejection}[{string.Join("; ", result.Events.Select(Event))}]";

    public static string Event(GameEvent gameEvent)
    {
        var properties = gameEvent.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={Value(p.GetValue(gameEvent))}");
        return $"{gameEvent.GetType().Name}({string.Join(", ", properties)})";
    }

    private static string Value(object? value) => value switch
    {
        IEnumerable<string> items => string.Join("|", items),
        IEnumerable<Hex> tiles => string.Join("|", tiles),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "null",
    };
}
