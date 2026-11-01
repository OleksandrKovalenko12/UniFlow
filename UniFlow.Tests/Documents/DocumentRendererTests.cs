using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using UniFlow.Documents.Rendering;

namespace UniFlow.Tests.Documents;

public class DocumentRendererTests
{
    private static DocumentRenderer CreateRenderer() => new(NullLoggerFactory.Instance);

    [Fact]
    public void Constructor_NullLoggerFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new DocumentRenderer(null!));

        Assert.Equal("loggerFactory", exception.ParamName);
    }

    [Fact]
    public async Task RenderAsync_ValidModel_RendersModelValues()
    {
        var renderer = CreateRenderer();

        var html = await renderer.RenderAsync<TestTemplate, TestModel>(new TestModel("Jan Kowalski"));

        Assert.Equal("""<p title="Jan Kowalski">Jan Kowalski</p>""", html);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>", "&lt;script&gt;alert(1)&lt;/script&gt;")]
    [InlineData("""<img src="http://evil.example/x.png">""", "&lt;img src=&quot;http://evil.example/x.png&quot;&gt;")]
    [InlineData("""x" onmouseover="alert(1)""", "x&quot; onmouseover=&quot;alert(1)")]
    [InlineData("Kowalski & Nowak", "Kowalski &amp; Nowak")]
    public async Task RenderAsync_HtmlInModel_IsEscapedInTextAndAttribute(string text, string expected)
    {
        var renderer = CreateRenderer();

        var html = await renderer.RenderAsync<TestTemplate, TestModel>(new TestModel(text));

        Assert.Equal($"""<p title="{expected}">{expected}</p>""", html);
    }

    [Fact]
    public async Task RenderAsync_PolishCharacters_AreNotEncoded()
    {
        var renderer = CreateRenderer();

        var html = await renderer.RenderAsync<TestTemplate, TestModel>(new TestModel("Zażółć gęślą jaźń ŁÓDŹ"));

        Assert.Equal("""<p title="Zażółć gęślą jaźń ŁÓDŹ">Zażółć gęślą jaźń ŁÓDŹ</p>""", html);
    }

    [Fact]
    public async Task RenderAsync_NullModel_ThrowsArgumentNullException()
    {
        var renderer = CreateRenderer();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => renderer.RenderAsync<TestTemplate, TestModel>(null!));

        Assert.Equal("model", exception.ParamName);
    }

    [Fact]
    public async Task RenderAsync_CalledTwice_RendersEachModelSeparately()
    {
        var renderer = CreateRenderer();

        var first = await renderer.RenderAsync<TestTemplate, TestModel>(new TestModel("Pierwszy"));
        var second = await renderer.RenderAsync<TestTemplate, TestModel>(new TestModel("Drugi"));

        Assert.Equal("""<p title="Pierwszy">Pierwszy</p>""", first);
        Assert.Equal("""<p title="Drugi">Drugi</p>""", second);
    }

    private sealed record TestModel(string Text);

    // Equivalent of a .razor file: <p title="@Model.Text">@Model.Text</p>
    private sealed class TestTemplate : ComponentBase, IDocumentTemplate<TestModel>
    {
        [Parameter]
        public TestModel Model { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "title", Model.Text);
            builder.AddContent(2, Model.Text);
            builder.CloseElement();
        }
    }
}