using UniFlow.Api.Applications;
using UniFlow.Shared.Applications;

namespace UniFlow.Tests.Api;

public class FileTypesTests
{
    // The first bytes of a real file of each type
    private static readonly Dictionary<string, byte[]> Headers = new()
    {
        [".pdf"] = "%PDF-1.7\n%ab"u8.ToArray(),
        [".docx"] = [0x50, 0x4B, 0x03, 0x04, 0x14, 0, 0, 0, 0, 0, 0, 0],
        [".odt"] = [0x50, 0x4B, 0x03, 0x04, 0x14, 0, 0, 0, 0, 0, 0, 0],
        [".jpg"] = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1],
        [".jpeg"] = [0xFF, 0xD8, 0xFF, 0xE1, 0, 0x10, 0x45, 0x78, 0x69, 0x66, 0, 0],
        [".png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D],
        [".webp"] = "RIFF\x24\0\0\0WEBP"u8.ToArray(),
    };

    // Every extension the browser offers must be recognised by the server
    [Fact]
    public void Detect_EveryAllowedExtension_IsRecognised()
    {
        foreach (var extension in AttachmentRules.Extensions)
            Assert.NotNull(FileTypes.Detect("plik" + extension, Headers[extension]));
    }

    [Theory]
    [InlineData("Skan.JPEG", "image/jpeg", ".jpg")]
    [InlineData("umowa.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx")]
    [InlineData("umowa.odt", "application/vnd.oasis.opendocument.text", ".odt")]
    public void Detect_ReturnsTypeAndDiskExtension(string fileName, string contentType, string extension)
    {
        var header = Headers[Path.GetExtension(fileName).ToLowerInvariant()];

        Assert.Equal(new DetectedFileType(contentType, extension), FileTypes.Detect(fileName, header));
    }

    [Theory]
    [InlineData("zdjecie.jpg", ".png")]     // content of another allowed type
    [InlineData("dokument.pdf", ".docx")]
    [InlineData("zdjecie.webp", ".pdf")]
    [InlineData("program.exe", ".pdf")]     // extension not on the list
    [InlineData("bez-rozszerzenia", ".pdf")]
    public void Detect_ContentDoesNotMatchName_ReturnsNull(string fileName, string headerOf)
    {
        Assert.Null(FileTypes.Detect(fileName, Headers[headerOf]));
    }

    [Fact]
    public void Detect_HeicRenamedToJpg_ReturnsNull()
    {
        byte[] heic = [0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63];   // "ftypheic"

        Assert.Null(FileTypes.Detect("IMG_0001.jpg", heic));
    }

    [Fact]
    public void Detect_FileShorterThanSignature_ReturnsNull()
    {
        Assert.Null(FileTypes.Detect("a.webp", "RIFF"u8));
        Assert.Null(FileTypes.Detect("a.pdf", []));
    }
}