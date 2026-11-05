using System.Reflection;
using System.Runtime.CompilerServices;
using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// The texts one module declares, found by scanning its types: every public static field or property of type
/// <see cref="TextDefinition"/> or <see cref="FieldText"/> (and the three definitions of each field) on a public
/// static class, nested ones included. <see cref="TextCatalog"/> checks each against the source's module segment.
/// </summary>
public sealed class TextSource
{
    private const string CommonModule = "common";

    private TextSource(string moduleSegment, IReadOnlyList<TextCatalogEntry> entries, IReadOnlyList<FieldText> fields)
    {
        ModuleSegment = moduleSegment;
        Entries = entries;
        Fields = fields;
    }

    /// <summary>SharedKernel's own text classes; module segment <c>common</c>.</summary>
    public static TextSource Common { get; } = FromAssembly(typeof(TextSource).Assembly, CommonModule);

    /// <summary>The module segment keys of this source must start with (kebab case).</summary>
    public string ModuleSegment { get; }

    internal IReadOnlyList<TextCatalogEntry> Entries { get; }

    internal IReadOnlyList<FieldText> Fields { get; }

    public static TextSource FromAssembly(Assembly assembly, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return FromTypes(moduleName, assembly.GetTypes());
    }

    public static TextSource FromTypes(string moduleName, params Type[] declaringTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(declaringTypes);

        string segment = SerginStringKey.ToSegment(moduleName);
        List<TextCatalogEntry> entries = [];
        List<FieldText> fields = [];

        foreach (Type type in declaringTypes.Where(IsDeclaringType))
        {
            Collect(type, entries, fields);
        }

        return new TextSource(segment, entries, fields);
    }

    private static bool IsDeclaringType(Type type) =>
        type.IsAbstract && type.IsSealed && (type.IsPublic || type.IsNestedPublic) && !type.IsGenericTypeDefinition;

    private static void Collect(Type type, List<TextCatalogEntry> entries, List<FieldText> fields)
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            foreach (object? value in MembersOf(type))
            {
                switch (value)
                {
                    case TextDefinition definition:
                        entries.Add(EntryOf(definition, [.. definition.Key.Segments.Skip(1).SkipLast(1)], type));
                        break;
                    case FieldText field:
                        fields.Add(field);
                        foreach (TextDefinition? part in new[] { field.Label, field.Hint, field.Placeholder })
                        {
                            if (part is not null)
                            {
                                entries.Add(EntryOf(part, field.Category.Path, type));
                            }
                        }

                        break;
                    default:
                        break;
                }
            }
        }
        catch (Exception error) when (error is TypeInitializationException or TargetInvocationException)
        {
            throw new InvalidOperationException(
                $"{type.FullName} declares a text that cannot be built: {(error.InnerException ?? error).Message}", error);
        }
    }

    private static IEnumerable<object?> MembersOf(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in type.GetFields(flags).Where(f => IsText(f.FieldType)))
        {
            yield return field.GetValue(null);
        }

        foreach (PropertyInfo property in type.GetProperties(flags).Where(p => IsText(p.PropertyType) && p.GetIndexParameters().Length == 0))
        {
            yield return property.GetValue(null);
        }
    }

    private static bool IsText(Type type) => type == typeof(TextDefinition) || type == typeof(FieldText);

    private static TextCatalogEntry EntryOf(TextDefinition definition, IReadOnlyList<string> category, Type declaringType) =>
        new(definition, definition.Key.Module, category, declaringType);
}
