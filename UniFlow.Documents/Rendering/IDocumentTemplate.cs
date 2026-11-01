namespace UniFlow.Documents.Rendering;

public interface IDocumentTemplate<TModel>
{
    TModel Model { get; }
}