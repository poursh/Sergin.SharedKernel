// Kept in the start command's namespace when it moved here: every caller that names it already imports that one.
namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>A run's identity outside its module: the schema whose store holds it, and its id.</summary>
public sealed record TaskRunRef(string Schema, Guid RunId);
