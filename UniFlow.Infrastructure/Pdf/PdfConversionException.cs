namespace UniFlow.Infrastructure.Pdf;

public sealed class PdfConversionException : Exception
{
    // True when Gotenberg answered with an error: the file itself may be broken.
    // False when it could not be reached or did not answer in time.
    public bool FileRejected { get; }

    public PdfConversionException(string message, Exception? innerException = null,
        bool fileRejected = false)
        : base(message, innerException)
    {
        FileRejected = fileRejected;
    }
}