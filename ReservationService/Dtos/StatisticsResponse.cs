namespace ReservationService.Dtos;

public class StatisticsResponse
{
    public Guid UserId { get; set; }
    public int ActiveReservations { get; set; }
    public int BorrowingHistory { get; set; }
}
