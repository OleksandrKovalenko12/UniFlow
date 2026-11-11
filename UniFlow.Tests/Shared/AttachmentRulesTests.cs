using UniFlow.Domain.Entities;
using UniFlow.Shared.Applications;

namespace UniFlow.Tests.Shared;

public class AttachmentRulesTests
{
    // The browser checks with Shared, the server with Domain - both must say the same
    [Fact]
    public void MaxSizeBytes_IsTheSameAsInDomain()
    {
        Assert.Equal(Attachment.MaxSizeBytes, AttachmentRules.MaxSizeBytes);
    }

    [Theory]
    [InlineData("skan.pdf", true)]
    [InlineData("ZDJECIE.JPG", true)]
    [InlineData("IMG_0001.heic", false)]
    [InlineData("stary.doc", false)]
    [InlineData("bez-rozszerzenia", false)]
    public void IsAllowedExtension_IgnoresCase(string fileName, bool allowed)
    {
        Assert.Equal(allowed, AttachmentRules.IsAllowedExtension(fileName));
    }
}