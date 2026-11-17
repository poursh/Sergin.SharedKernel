using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Validation;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>What became of a root set-property command a detail view sent.</summary>
internal enum SetPropertyStatus
{
    /// <summary>The command ran.</summary>
    Sent,

    /// <summary>Someone changed the record after the view read it: nothing was saved.</summary>
    Stale,

    /// <summary>The pipeline refused the command, or the send failed otherwise.</summary>
    Failed,

    /// <summary>The validator refused the value before anything was sent.</summary>
    Invalid,
}

/// <summary>One send's outcome: its status and its own errors (empty when <see cref="SetPropertyStatus.Sent"/>).</summary>
internal sealed record SetPropertyOutcome(SetPropertyStatus Status, IReadOnlyList<Error> Errors);

/// <summary>
/// The decisions behind a root's set-property command, apart from the views that offer it (a dialog, an inline card, an
/// inline choice): the command built from the record's id and one value, that value checked by the pipeline's own
/// validator before anything is sent, then the send, at the held version when the command is version-guarded. Each
/// <see cref="SendAsync"/> answers its own <see cref="SetPropertyOutcome"/>, so two sends in flight never share errors;
/// the view shows them and reads the record again.
/// </summary>
internal sealed class GeneratedSetProperty(ISerginFormValidator validator)
{
    /// <summary>The command <paramref name="command"/> sets <paramref name="value"/> with on the record <paramref name="id"/>.</summary>
    public static object Build(CommandView command, Guid id, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);

        FormFieldView field = ValueFieldOf(command);

        return command.BuildFromValues!(id, new Dictionary<string, object?>(StringComparer.Ordinal) { [field.Name] = value });
    }

    /// <summary>
    /// The pipeline validator's messages for <paramref name="propertyName"/> (the value field when null) of the command
    /// built with <paramref name="value"/>: what a value form shows as the user types.
    /// </summary>
    public Task<IReadOnlyCollection<string>> ValidateAsync(CommandView command, Guid id, object? value, string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Invoker.ValidateAsync(validator, Build(command, id, value), propertyName ?? ValueFieldOf(command).Name);
    }

    /// <summary>
    /// Sends the command setting <paramref name="value"/>, unless the validator refuses the value (then nothing is sent and
    /// its messages are the errors): versioned at <paramref name="version"/> when the command requires one, plainly otherwise.
    /// </summary>
    public async Task<SetPropertyOutcome> SendAsync(
        ISerginDispatcher dispatcher, CommandView command, Guid id, object? value, RowVersion? version)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(command);

        string name = ValueFieldOf(command).Name;
        IReadOnlyCollection<string> messages = await ValidateAsync(command, id, value);

        if (messages.Count > 0)
        {
            return new SetPropertyOutcome(SetPropertyStatus.Invalid, [.. messages.Select(message => Error.Validation(name, message))]);
        }

        object request = Build(command, id, value);
        ViewSendResult result = command.RequiresVersion
            ? await command.Invoker.SendVersionedAsync(dispatcher, request, version)
            : await command.Invoker.SendAsync(dispatcher, request);

        if (!result.IsError)
        {
            return new SetPropertyOutcome(SetPropertyStatus.Sent, []);
        }

        return new SetPropertyOutcome(
            result.Errors.Any(VersionErrors.IsStale) ? SetPropertyStatus.Stale : SetPropertyStatus.Failed, result.Errors);
    }

    private static FormFieldView ValueFieldOf(CommandView command) =>
        command.ValueField ?? throw new InvalidOperationException(
            $"{command.Request.FullName} has no value field: only a root's shown set-property command is sent with one value.");
}
