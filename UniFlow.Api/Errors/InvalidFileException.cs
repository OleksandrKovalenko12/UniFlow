namespace UniFlow.Api.Errors;

// A file the student sent cannot be accepted; the code tells the Client which message to show
public sealed class InvalidFileException : Exception
{
    public string Code { get; }

    public InvalidFileException(string code, string message) : base(message)
    {
        Code = code;
    }
}