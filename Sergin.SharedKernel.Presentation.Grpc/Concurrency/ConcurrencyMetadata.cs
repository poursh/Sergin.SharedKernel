namespace Sergin.SharedKernel.Presentation.Grpc.Concurrency;

/// <summary>
/// The gRPC metadata keys carrying <see cref="Application.Concurrency.ConcurrencyContext"/> across a Remote
/// call: the expected version as a request header, the version the call read or wrote as a response trailer (a
/// trailer, because the server only knows it once the handler has run).
/// </summary>
public static class ConcurrencyMetadata
{
    public const string ExpectedVersionKey = "sergin-expected-version";
    public const string VersionKey = "sergin-version";
}
