using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UniFlow.Documents.Rendering;

public sealed class DocumentRenderer
{
    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;

    public DocumentRenderer(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _loggerFactory = loggerFactory;

        // Escapes <, >, &, " but keeps Polish letters readable
        _services = new ServiceCollection()
            .AddSingleton(HtmlEncoder.Create(UnicodeRanges.All))
            .BuildServiceProvider();
    }

    public async Task<string> RenderAsync<TTemplate, TModel>(TModel model)
        where TTemplate : IComponent, IDocumentTemplate<TModel>
    {
        ArgumentNullException.ThrowIfNull(model);

        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(IDocumentTemplate<TModel>.Model)] = model
        });

        await using var renderer = new HtmlRenderer(_services, _loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TTemplate>(parameters);
            return output.ToHtmlString();
        });
    }
}