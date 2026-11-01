namespace UniFlow.Infrastructure.Pdf;

public sealed class PdfConversionException : Exception
{
    public PdfConversionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}