using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace GitHub.Accelerator.Core;

public sealed class StatusResponse
{
    public string proxy_addr { get; init; } = string.Empty;
    public string gui_addr { get; init; } = string.Empty;
    public bool github_only { get; init; }
    public string started_at { get; init; } = string.Empty;
    public GitProxyStatusResponse git_proxy { get; init; } = new();
}

public sealed class GitProxyStatusResponse
{
    public bool enabled { get; init; }
    public string http_proxy { get; init; } = string.Empty;
    public string https_proxy { get; init; } = string.Empty;
}

public sealed class TestDomainResponse
{
    public bool ok { get; init; }
    public bool blocked { get; init; }
    public int status { get; init; }
    public long ms { get; init; }
    public string message { get; init; } = string.Empty;
    public string error { get; init; } = string.Empty;
}

public sealed class GitActionResponse
{
    public bool ok { get; init; }
    public GitProxyStatusResponse status { get; init; } = new();
}

[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(TestDomainResponse))]
[JsonSerializable(typeof(GitActionResponse))]
[JsonSerializable(typeof(GitProxyStatusResponse))]
public partial class ApiJsonContext : JsonSerializerContext
{
}

public static class ApiJson
{
    public static IResult Result<T>(T value, JsonTypeInfo<T> info)
    {
        var json = JsonSerializer.Serialize(value, info);
        return Results.Text(json, "application/json; charset=utf-8");
    }
}
