namespace ReservationService.Domain.Enums
{
    public enum ReservationState
    {
        Unknown = 0,
        Active = 1,
        Consuming = 2,
        Consumed = 3,
        Canceled = 4,
        Expired = 5
    }
}
