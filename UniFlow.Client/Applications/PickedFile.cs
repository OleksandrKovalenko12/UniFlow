namespace UniFlow.Client.Applications;

// A file chosen in the form. Its content is read at once: the browser forgets
// the previous selection as soon as the student picks more files.
public sealed record PickedFile(string Name, byte[] Content)
{
    public long Size => Content.LongLength;
}