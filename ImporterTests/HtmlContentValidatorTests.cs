using Importer.Security;

namespace ImporterTests;

[TestFixture]
public class HtmlContentValidatorTests
{
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("plain text", true)]
    [TestCase("<p>ok</p>", true)]
    [TestCase("<div class=\"x\">y</div>", true)]
    [TestCase("<b>x</b>", true)]
    [TestCase("<br/>", true)]
    [TestCase("<br />", true)]
    [TestCase("<img src=\"/api/Attachments/1\">", true)]
    [TestCase("<span class=\"mention\">x</span>", true)]
    [TestCase("<SOAP-ENV:Envelope xmlns:x=\"y\">", true)]
    [TestCase("<custom-element>z</custom-element>", true)]
    [TestCase("</script>", true)]
    [TestCase("before</script>after", true)]
    [TestCase("<not-a-tag 123", true)]
    [TestCase("< script>", true)]
    [TestCase("<script>x</script>", false)]
    [TestCase("<SCRIPT>case</SCRIPT>", false)]
    [TestCase("<style>x</style>", false)]
    [TestCase("<iframe src=\"x\"></iframe>", false)]
    [TestCase("<embed src=\"x\">", false)]
    [TestCase("<object data=\"x\">", false)]
    [TestCase("<link rel=\"x\">", false)]
    [TestCase("<base href=\"/\">", false)]
    [TestCase("<applet></applet>", false)]
    [TestCase("<audio></audio>", false)]
    [TestCase("<video></video>", false)]
    [TestCase("<canvas></canvas>", false)]
    [TestCase("<noscript></noscript>", false)]
    [TestCase("<param name=\"x\">", false)]
    [TestCase("<source src=\"x\">", false)]
    [TestCase("<track src=\"x\">", false)]
    [TestCase("<dialog></dialog>", false)]
    [TestCase("<frameset></frameset>", false)]
    [TestCase("<frame src=\"x\">", false)]
    [TestCase("<div onclick=\"1\">x</div>", false)]
    [TestCase("<div ONCLICK=\"1\">x</div>", false)]
    [TestCase("<div onload=\"1\">x</div>", false)]
    [TestCase("<div onerror=\"1\">x</div>", false)]
    [TestCase("<div onwheel=\"1\">x</div>", false)]
    [TestCase("<div data-onclick=\"safe\">x</div>", true)]
    [TestCase("<a href=\"javascript:alert(1)\">x</a>", false)]
    [TestCase("<a href='javascript:void(0)'>x</a>", false)]
    [TestCase("<a href=\"JavaScript:alert(1)\">x</a>", false)]
    [TestCase("<a href=\"https://example.com\">x</a>", true)]
    [TestCase("<a href=\"notjavascript:alert(1)\">x</a>", true)]
    [TestCase("<a href=\"javascriptx:alert(1)\">x</a>", true)]
    public void IsValid_MatchesBackendRules(string? html, bool expectedValid) =>
        Assert.That(HtmlContentValidator.IsValid(html), Is.EqualTo(expectedValid));

    [TestCase("<p>ok</p>", -1)]
    [TestCase("<script>x</script>", 0)]
    [TestCase("a<script>x</script>", 1)]
    [TestCase("<p>a</p><style>x</style>", 8)]
    public void FindFirstInvalidOpenBracketIndex_PointsAtFirstBadTag(string html, int expectedIndex)
    {
        Assert.That(HtmlContentValidator.FindFirstInvalidOpenBracketIndex(html), Is.EqualTo(expectedIndex));
    }

    [Test]
    public void SanitizeToPassValidator_LeavesSafeMarkupUnchanged() =>
        Assert.That(HtmlContentSecuritySanitizer.SanitizeToPassValidator("<p>a</p><b>b</b>"), Is.EqualTo("<p>a</p><b>b</b>"));

    [Test]
    public void SanitizeToPassValidator_EscapesInsecureTagOpeners()
    {
        var s = HtmlContentSecuritySanitizer.SanitizeToPassValidator("<p>a</p><script>x</script>");
        Assert.Multiple(() =>
        {
            Assert.That(s, Does.Contain("<p>a</p>"));
            Assert.That(s, Does.Contain("&lt;script"));
            Assert.That(HtmlContentValidator.IsValid(s), Is.True);
        });
    }

    [Test]
    public void SanitizeToPassValidator_FixesMultipleInsecureTagsSequentially()
    {
        var s = HtmlContentSecuritySanitizer.SanitizeToPassValidator("<script>a</script><style>b</style>");
        Assert.Multiple(() =>
        {
            Assert.That(HtmlContentValidator.IsValid(s), Is.True);
            Assert.That(s, Does.Contain("&lt;script"));
            Assert.That(s, Does.Contain("&lt;style"));
        });
    }

    [Test]
    public void SanitizeToPassValidator_FixesOnclickThenLeavesRest()
    {
        var s = HtmlContentSecuritySanitizer.SanitizeToPassValidator("<div onclick=\"x\">y</div>");
        Assert.Multiple(() =>
        {
            Assert.That(HtmlContentValidator.IsValid(s), Is.True);
            Assert.That(s, Does.StartWith("&lt;div onclick="));
        });
    }

    [Test]
    public void SanitizeToPassValidator_FixesJavascriptHref()
    {
        var s = HtmlContentSecuritySanitizer.SanitizeToPassValidator("<a href=\"javascript:alert(1)\">x</a>");
        Assert.Multiple(() =>
        {
            Assert.That(HtmlContentValidator.IsValid(s), Is.True);
            Assert.That(s, Does.StartWith("&lt;a href="));
        });
    }

    [Test]
    public void SanitizeToPassValidator_KeepsNestedSafeTagsInsideEscapedBlock()
    {
        var raw = "<script><p>inside</p></script>";
        var s = HtmlContentSecuritySanitizer.SanitizeToPassValidator(raw);
        Assert.Multiple(() =>
        {
            Assert.That(HtmlContentValidator.IsValid(s), Is.True);
            Assert.That(s, Does.StartWith("&lt;script>"));
        });
    }
}
