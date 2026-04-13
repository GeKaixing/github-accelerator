namespace GitHub.Accelerator.Core;

public static class GitHubDomainRules
{
    private static readonly string[] Suffixes =
    [
        "github.com",
        "githubusercontent.com",
        "githubassets.com",
        "github.dev",
        "github.io",
        "githubapp.com",
        "ghcr.io",
    ];

    public static bool IsGitHubHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        host = host.Trim().ToLowerInvariant();
        foreach (var suffix in Suffixes)
        {
            if (host == suffix || host.EndsWith('.' + suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
