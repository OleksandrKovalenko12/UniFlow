using System.ComponentModel.DataAnnotations;

namespace UniFlow.Shared.Review;

// Correction, rejection and returning to the dean's office: the comment explains the decision,
// so it cannot be empty
public sealed class CommentRequest
{
    public const int MaxLength = 2000;

    [Required(ErrorMessage = "Wpisz komentarz.")]
    [MaxLength(MaxLength, ErrorMessage = "Komentarz może mieć najwyżej 2000 znaków.")]
    public string Comment { get; set; } = "";
}

// Forwarding to the vice-dean: an internal note is optional
public sealed class NoteRequest
{
    [MaxLength(CommentRequest.MaxLength, ErrorMessage = "Notatka może mieć najwyżej 2000 znaków.")]
    public string? Comment { get; set; }
}