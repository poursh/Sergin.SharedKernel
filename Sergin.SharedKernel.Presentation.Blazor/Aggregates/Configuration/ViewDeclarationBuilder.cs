using Sergin.SharedKernel.Application.Commands;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// What a root and each of its child entities declare the same way: commands, detail and list field overrides, and forms.
/// The registry reads the collected declarations through this non-generic half.
/// </summary>
public abstract class ViewDeclarationBuilder
{
    private protected ViewDeclarationBuilder()
    {
    }

    /// <summary>The commands the configuration named, by request type.</summary>
    internal Dictionary<Type, CommandViewBuilder> Commands { get; } = [];

    internal FieldSetBuilder? DetailFields { get; private protected set; }

    internal FieldSetBuilder? ListFields { get; private protected set; }

    /// <summary>The forms the configuration named, by command type.</summary>
    internal Dictionary<Type, FormViewBuilder> Forms { get; } = [];

    private protected CommandViewBuilder<TCommand> DeclareCommand<TCommand>()
        where TCommand : IBaseCommand
    {
        if (Commands.TryGetValue(typeof(TCommand), out CommandViewBuilder? existing))
        {
            return (CommandViewBuilder<TCommand>)existing;
        }

        CommandViewBuilder<TCommand> command = new();
        Commands.Add(typeof(TCommand), command);
        return command;
    }

    private protected void DeclareDetail<TResponse>(Action<FieldSetBuilder<TResponse>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        FieldSetBuilder<TResponse> fields = DetailFields as FieldSetBuilder<TResponse> ?? new FieldSetBuilder<TResponse>();
        configure(fields);
        DetailFields = fields;
    }

    private protected void DeclareList<TItem>(Action<ListViewBuilder<TItem>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ListViewBuilder<TItem> fields = ListFields as ListViewBuilder<TItem> ?? new ListViewBuilder<TItem>();
        configure(fields);
        ListFields = fields;
    }

    private protected void DeclareForm<TCommand>(Action<FormViewBuilder<TCommand>> configure)
        where TCommand : IBaseCommand
    {
        ArgumentNullException.ThrowIfNull(configure);

        if (!Forms.TryGetValue(typeof(TCommand), out FormViewBuilder? form))
        {
            form = new FormViewBuilder<TCommand>();
            Forms.Add(typeof(TCommand), form);
        }

        configure((FormViewBuilder<TCommand>)form);
    }
}
