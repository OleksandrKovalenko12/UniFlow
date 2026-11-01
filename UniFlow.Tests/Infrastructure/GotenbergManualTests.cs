using UniFlow.Infrastructure.Pdf;

namespace UniFlow.Tests.Infrastructure;

public class GotenbergManualTests
{
    private const string ManualReason =
        "Manual check: requires running Gotenberg (docker compose up -d) and files in SamplesFolder.";
    private const string SamplesFolder = @"C:\uniflow-samples";

    private static readonly Dictionary<string, string> ContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".odt"] = "application/vnd.oasis.opendocument.text",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        };

    //(Skip = ManualReason)
    [Fact(Skip = ManualReason)]
    public async Task ConvertAllSamples()
    {
        var converter = new GotenbergPdfConverter(new HttpClient
        {
            BaseAddress = new Uri("http://localhost:3000/"),
            Timeout = TimeSpan.FromSeconds(60)
        });

        var outputFolder = Path.Combine(SamplesFolder, "output");
        Directory.CreateDirectory(outputFolder);

        var samples = Directory.GetFiles(SamplesFolder)
            .Where(path => ContentTypes.ContainsKey(Path.GetExtension(path)))
            .ToList();

        Assert.NotEmpty(samples);

        foreach (var path in samples)
        {
            await using var input = File.OpenRead(path);
            var pdf = await converter.ConvertToPdfAsync(input, ContentTypes[Path.GetExtension(path)]);

            Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);

            var outputPath = Path.Combine(outputFolder, Path.GetFileName(path) + ".pdf");
            await File.WriteAllBytesAsync(outputPath, pdf);
        }
    }
}