namespace UniFlow.Client.Applications;

public enum UploadStatus
{
    Waiting,
    Sending,
    Added,
    Failed
}

// One picked file on its way to the server after the application was submitted
public sealed class AttachmentUpload(PickedFile file)
{
    public PickedFile File { get; } = file;
    public UploadStatus Status { get; set; } = UploadStatus.Waiting;
    public Guid? AttachmentId { get; set; }
    public string? Error { get; set; }
}