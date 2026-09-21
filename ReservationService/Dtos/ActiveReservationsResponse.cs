using ReservationService.Models;

namespace ReservationService.Dtos;

public class ActiveReservationsResponse
{
    public List<ActiveReservationEntry> Reservations { get; set; } = new();
    public int TotalActive { get; set; }
}

public class ActiveReservationEntry
{
    public Guid ReservationId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; }
    public DateTime? ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int? DaysUntilDue { get; set; }
}
