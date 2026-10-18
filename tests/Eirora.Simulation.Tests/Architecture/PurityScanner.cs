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
        // Reflection reveals which runtime and assemblies run the simulation.
        "System.Reflection",
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

        // A type's name, which the simulation only puts in exception messages.
        "System.Reflection.MemberInfo::get_Name",
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

    // Number types whose formatting and parsing follow the current culture unless given a format provider.
    private static readonly HashSet<string> s_numberTypes =
    [
        "System.Byte",
        "System.SByte",
        "System.Int16",
        "System.UInt16",
        "System.Int32",
        "System.UInt32",
        "System.Int64",
        "System.UInt64",
        "System.Decimal",
    ];

    // String members that compare or change case by the current culture unless given a StringComparison.
    private static readonly HashSet<string> s_cultureStringMembers =
        ["Compare", "CompareTo", "StartsWith", "EndsWith", "IndexOf", "LastIndexOf", "ToLower", "ToUpper"];

    // Enumerations whose order depends on hashing and on the runtime's implementation of the collection.
    private static readonly HashSet<string> s_hashOrderMembers =
    [
        "System.Collections.Generic.Dictionary`2::GetEnumerator",
        "System.Collections.Generic.Dictionary`2::get_Keys",
        "System.Collections.Generic.Dictionary`2::get_Values",
        "System.Collections.Generic.HashSet`1::GetEnumerator",
    ];

    // Sorted collections whose default comparer for strings follows the current culture.
    private static readonly HashSet<string> s_sortedCollections =
    [
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2",
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
                    CheckOrderAndCulture(called, where, violations);
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

    /// <summary>
    /// Catches what gives a different result on another runtime or device (INV-D1, docs/development.md#determinism):
    /// culture-dependent formatting, parsing and string comparison, and enumeration in hash order.
    /// </summary>
    private static void CheckOrderAndCulture(MethodReference called, string where, List<string> violations)
    {
        string type = called.DeclaringType.GetElementType().FullName;
        string member = $"{type}::{called.Name}";
        bool Takes(string parameterType) => called.Parameters.Any(p => p.ParameterType.FullName == parameterType);

        if (s_numberTypes.Contains(type) && called.Name is "ToString" or "Parse" or "TryParse" && !Takes("System.IFormatProvider"))
        {
            violations.Add($"{where}: culture-dependent {member}");
        }

        bool byCharacter = called.Parameters.Count > 0 && called.Parameters[0].ParameterType.FullName == "System.Char";
        if (type == "System.String" && s_cultureStringMembers.Contains(called.Name) && !Takes("System.StringComparison") && !byCharacter)
        {
            violations.Add($"{where}: culture-dependent {member}");
        }

        if (s_hashOrderMembers.Contains(member))
        {
            violations.Add($"{where}: hash order {member}");
        }

        if (called.Name == ".ctor"
            && s_sortedCollections.Contains(type)
            && called.DeclaringType is GenericInstanceType sorted
            && sorted.GenericArguments[0].FullName == "System.String"
            && !called.Parameters.Any(p => p.ParameterType.GetElementType().FullName == "System.Collections.Generic.IComparer`1"))
        {
            violations.Add($"{where}: culture-dependent {member} without a comparer");
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
