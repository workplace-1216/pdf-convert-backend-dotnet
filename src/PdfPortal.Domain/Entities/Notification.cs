namespace PdfPortal.Domain.Entities;

public class Notification
{
    public int Id { get; set; }
    public int ClientUserId { get; set; }
    public int DocumentCount { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public User? ClientUser { get; set; }
}

