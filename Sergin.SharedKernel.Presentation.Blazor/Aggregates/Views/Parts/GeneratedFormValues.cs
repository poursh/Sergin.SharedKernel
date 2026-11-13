namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// A generated form's model: each shown field's value by name, as the primitive its input edits (the request factory
/// wraps it into the command's type), and each child picker's chosen parent, which is view state and never on the
/// command.
/// </summary>
public sealed class GeneratedFormValues
{
    private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Guid> parents = new(StringComparer.Ordinal);

    /// <summary>The values by field name: what <c>BuildCreate</c> / <c>BuildUpdate</c> take.</summary>
    public IReadOnlyDictionary<string, object?> Values => values;

    public object? ValueOf(string name) => values.GetValueOrDefault(name);

    public void Set(string name, object? value) => values[name] = value;

    /// <summary>The parent a child picker's options are listed under; <see cref="Guid.Empty"/> while none is chosen.</summary>
    public Guid ParentOf(string name) => parents.GetValueOrDefault(name);

    public void SetParent(string name, Guid parent) => parents[name] = parent;
}
