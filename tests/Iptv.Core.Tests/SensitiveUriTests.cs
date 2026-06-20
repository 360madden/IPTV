namespace Iptv.Core.Tests;

public sealed class SensitiveUriTests
{
    [Fact]
    public void ToString_RedactsCredentialsPathAndQueryValues()
    {
        bool created = SensitiveUri.TryCreate(
            "https://user:secret@example.com/private/account/live.m3u8?username=bob&password=letmein&token=abc123",
            out SensitiveUri? uri,
            out string? error);

        Assert.True(created, error);
        Assert.NotNull(uri);

        string redacted = uri.ToString();

        Assert.Contains("https://example.com", redacted);
        Assert.Contains("live.m3u8", redacted);
        Assert.DoesNotContain("secret", redacted);
        Assert.DoesNotContain("letmein", redacted);
        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("private/account", redacted);
    }

    [Fact]
    public void TryCreate_RejectsUnsupportedSchemes()
    {
        bool created = SensitiveUri.TryCreate("file:///c:/private.m3u8", out _, out string? error);

        Assert.False(created);
        Assert.Contains("Unsupported", error);
    }

    [Fact]
    public void RedactText_RedactsTokenLikeValues()
    {
        string redacted = Diagnostics.SensitiveTextRedactor.RedactText(
            "failed https://user:secret@example.com/live?token=abc123&quality=hd username=bob password=letmein");

        Assert.DoesNotContain("secret", redacted);
        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("bob", redacted);
        Assert.DoesNotContain("letmein", redacted);
        Assert.Contains("token=REDACTED", redacted);
    }
}
