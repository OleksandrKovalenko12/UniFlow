using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using UniFlow.Domain.Entities;
using UniFlow.Infrastructure.Storage;

namespace UniFlow.Api.Controllers;

// How every PDF leaves the server: the application, its attachments and the files of a draft
public static class PdfResponses
{
    public static FileStreamResult PdfFile(this ControllerBase controller, IFileStorage storage,
        string storagePath, string fileName, bool inline)
    {
        // "inline" opens the PDF in the browser, "attachment" saves it
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(fileName);
        controller.Response.Headers.ContentDisposition = disposition.ToString();

        // The browser must not guess another type than the one we send
        controller.Response.Headers.XContentTypeOptions = "nosniff";

        return controller.File(storage.OpenRead(storagePath), Attachment.PdfContentType);
    }
}