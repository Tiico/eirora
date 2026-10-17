using System.Reflection;
using System.Reflection.Emit;
using Eirora.Simulation.Forces;

namespace Eirora.Simulation.Tests;

/// <summary>
/// The simulation is shared by Unity and the server, so it may depend on nothing but .NET Standard (ADR-0002, ADR-0004),
/// and it must be deterministic on every platform, so it may not use floating point (INV-D3).
/// Clock, randomness and I/O are banned at compile time by BannedSymbols.txt (INV-D2).
/// </summary>
public class ArchitectureTests
{
    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type[] FloatingPointTypes = [typeof(float), typeof(double), typeof(decimal)];

    private static readonly HashSet<short> FloatingPointOpCodes =
    [
        OpCodes.Ldc_R4.Value, OpCodes.Ldc_R8.Value, OpCodes.Conv_R4.Value, OpCodes.Conv_R8.Value, OpCodes.Conv_R_Un.Value,
        OpCodes.Ldind_R4.Value, OpCodes.Ldind_R8.Value, OpCodes.Stind_R4.Value, OpCodes.Stind_R8.Value,
        OpCodes.Ldelem_R4.Value, OpCodes.Ldelem_R8.Value, OpCodes.Stelem_R4.Value, OpCodes.Stelem_R8.Value, OpCodes.Ckfinite.Value,
    ];

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(op => op.Value);

    private static Assembly Simulation => typeof(ForceRules).Assembly;

    [Fact]
    public void Simulation_references_only_dotnet_standard()
    {
        var references = Simulation.GetReferencedAssemblies().Select(a => a.Name);

        Assert.All(references, name => Assert.Equal("netstandard", name));
    }

    [Fact]
    public void Simulation_declares_no_floating_point_members()
    {
        var offenders = Simulation.GetTypes()
            .SelectMany(type => type.GetMembers(Everything).Select(member => (type, member)))
            .Where(x => MemberTypes(x.member).Any(IsFloatingPoint))
            .Select(x => $"{x.type.FullName}.{x.member.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void Simulation_executes_no_floating_point_instructions()
    {
        var offenders = Simulation.GetTypes()
            .SelectMany(type => type.GetMethods(Everything).Cast<MethodBase>().Concat(type.GetConstructors(Everything)))
            .Where(method => method.GetMethodBody() is { } body
                && (UsesFloatingPoint(body.GetILAsByteArray()!) || body.LocalVariables.Any(l => IsFloatingPoint(l.LocalType))))
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name}");

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scan_detects_floating_point()
    {
        var method = typeof(ArchitectureTests).GetMethod(nameof(Half), BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.True(UsesFloatingPoint(method.GetMethodBody()!.GetILAsByteArray()!));
    }

    private static double Half(int value) => value / 2.0;

    private static bool IsFloatingPoint(Type type) =>
        FloatingPointTypes.Contains(type)
        || (type.HasElementType && IsFloatingPoint(type.GetElementType()!))
        || (type.IsGenericType && type.GetGenericArguments().Any(IsFloatingPoint));

    private static IEnumerable<Type> MemberTypes(MemberInfo member) => member switch
    {
        FieldInfo field => [field.FieldType],
        PropertyInfo property => [property.PropertyType],
        MethodInfo method => method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType),
        ConstructorInfo constructor => constructor.GetParameters().Select(p => p.ParameterType),
        _ => [],
    };

    private static bool UsesFloatingPoint(byte[] il)
    {
        for (int i = 0; i < il.Length;)
        {
            short value = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i];
            var op = OpCodesByValue[value];
            if (FloatingPointOpCodes.Contains(op.Value))
            {
                return true;
            }

            i += op.Size + OperandSize(op, il, i + op.Size);
        }

        return false;
    }

    private static int OperandSize(OpCode op, byte[] il, int operandStart) => op.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, operandStart)),
        _ => 4,
    };
}
