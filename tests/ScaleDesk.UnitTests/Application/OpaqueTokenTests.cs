using ScaleDesk.Application.Common.Security;

namespace ScaleDesk.UnitTests.Application;

public class OpaqueTokenTests
{
    [Fact]
    public void Hash_is_deterministic_and_hex()
    {
        var a = OpaqueToken.Hash("same-input");
        var b = OpaqueToken.Hash("same-input");

        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{64}$", a); // SHA-256 => 64 hex chars, lowercase
    }

    [Fact]
    public void Generate_produces_url_safe_unique_tokens()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => OpaqueToken.Generate()).ToList();

        Assert.Equal(1000, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.DoesNotMatch("[+/=]", t));
    }
}
