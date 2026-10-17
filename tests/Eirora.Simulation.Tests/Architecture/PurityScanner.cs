using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Eirora.Simulation.Tests.Architecture;

/// <summary>
/// Scans compiled IL for anything that would make the simulation impure:
/// reading the outside world (INV-D2) or computing with floating point (INV-D3, forces.md).
/// </summary>
internal static class PurityScanner
{
    private static readonly string[] s_forbiddenNamespaces =
    [
        "System.IO",
        "System.Net",
        "UnityEngine",
        "UnityEditor",
        "Unity",
    ];

    private static readonly HashSet<string> s_forbiddenTypes =
    [
        "System.Random",
        "System.Environment",
        "System.Diagnostics.Stopwatch",
        "System.Threading.Thread",
        "System.Threading.Timer",
        "System.Timers.Timer",
        "System.Security.Cryptography.RandomNumberGenerator",
        "System.Math",
        "System.MathF",
        "System.Single",
        "System.Double",
    ];

    private static readonly HashSet<string> s_forbiddenMembers =
    [
        "System.DateTime::get_Now",
        "System.DateTime::get_UtcNow",
        "System.DateTime::get_Today",
        "System.DateTimeOffset::get_Now",
        "System.DateTimeOffset::get_UtcNow",
        "System.TimeZoneInfo::get_Local",
        "System.TimeZoneInfo::FindSystemTimeZoneById",
        "System.TimeZoneInfo::GetSystemTimeZones",
        "System.Guid::NewGuid",
        // String hash codes are randomised per process.
        "System.String::GetHashCode",
    ];

    // Members of forbidden types that cannot affect results.
    private static readonly HashSet<string> s_allowedMembers =
    [
        // Compiler-generated iterators (yield return) check which thread owns them.
        "System.Environment::get_CurrentManagedThreadId",
    ];

    private static readonly HashSet<Code> s_floatingPointOpCodes =
    [
        Code.Ldc_R4,
        Code.Ldc_R8,
        Code.Conv_R4,
        Code.Conv_R8,
        Code.Conv_R_Un,
        Code.Ldind_R4,
        Code.Ldind_R8,
        Code.Stind_R4,
        Code.Stind_R8,
        Code.Ldelem_R4,
        Code.Ldelem_R8,
        Code.Stelem_R4,
        Code.Stelem_R8,
        Code.Ckfinite,
    ];

    public static IReadOnlyList<string> FindViolations(IEnumerable<TypeDefinition> types)
    {
        var violations = new List<string>();
        foreach (var type in types.SelectMany(WithNestedTypes))
        {
            ScanType(type, violations);
        }

        return violations;
    }

    private static IEnumerable<TypeDefinition> WithNestedTypes(TypeDefinition type) =>
        type.NestedTypes.SelectMany(WithNestedTypes).Prepend(type);

    private static void ScanType(TypeDefinition type, List<string> violations)
    {
        if (type.BaseType is not null)
        {
            CheckType(type.BaseType, type.FullName, violations);
        }

        foreach (var implemented in type.Interfaces)
        {
            CheckType(implemented.InterfaceType, type.FullName, violations);
        }

        foreach (var field in type.Fields)
        {
            CheckType(field.FieldType, $"{type.FullName}.{field.Name}", violations);
        }

        foreach (var method in type.Methods)
        {
            ScanMethod(method, violations);
        }
    }

    private static void ScanMethod(MethodDefinition method, List<string> violations)
    {
        var where = method.FullName;
        CheckType(method.ReturnType, where, violations);
        foreach (var parameter in method.Parameters)
        {
            CheckType(parameter.ParameterType, where, violations);
        }

        if (!method.HasBody)
        {
            return;
        }

        foreach (var variable in method.Body.Variables)
        {
            CheckType(variable.VariableType, where, violations);
        }

        foreach (var instruction in method.Body.Instructions)
        {
            if (s_floatingPointOpCodes.Contains(instruction.OpCode.Code))
            {
                violations.Add($"{where}: floating-point instruction {instruction.OpCode.Name}");
            }

            switch (instruction.Operand)
            {
                case MethodReference called:
                    CheckMember(called.DeclaringType, called.Name, where, violations);
                    CheckType(called.ReturnType, where, violations);
                    foreach (var parameter in called.Parameters)
                    {
                        CheckType(parameter.ParameterType, where, violations);
                    }

                    break;
                case FieldReference field:
                    CheckMember(field.DeclaringType, field.Name, where, violations);
                    CheckType(field.FieldType, where, violations);
                    break;
                case TypeReference referenced:
                    CheckType(referenced, where, violations);
                    break;
            }
        }
    }

    private static void CheckMember(TypeReference declaringType, string name, string where, List<string> violations)
    {
        var member = $"{declaringType.FullName}::{name}";
        if (s_allowedMembers.Contains(member))
        {
            return;
        }

        CheckType(declaringType, where, violations);
        if (s_forbiddenMembers.Contains(member))
        {
            violations.Add($"{where}: uses {member}");
        }
    }

    private static void CheckType(TypeReference type, string where, List<string> violations)
    {
        // Generic parameters (T) carry no information until instantiated; instantiations are checked where they occur.
        if (type is GenericParameter)
        {
            return;
        }

        // Strips arrays, pointers, by-refs and generic instantiations.
        var element = type.GetElementType();
        if (s_forbiddenTypes.Contains(element.FullName) || IsInForbiddenNamespace(element))
        {
            violations.Add($"{where}: uses {element.FullName}");
        }

        if (type is GenericInstanceType generic)
        {
            foreach (var argument in generic.GenericArguments)
            {
                CheckType(argument, where, violations);
            }
        }
    }

    private static bool IsInForbiddenNamespace(TypeReference type)
    {
        var outermost = type;
        while (outermost.DeclaringType is not null)
        {
            outermost = outermost.DeclaringType;
        }

        var ns = outermost.Namespace;
        return s_forbiddenNamespaces.Any(forbidden =>
            ns == forbidden || ns.StartsWith(forbidden + ".", StringComparison.Ordinal));
    }
}
