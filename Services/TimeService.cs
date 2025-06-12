namespace govapi.Services
{
    public interface ITimeService
    {
        DateTime GetUtcNow();
    }

    public class SystemTimeService : ITimeService
    {
        public DateTime GetUtcNow() => DateTime.UtcNow;
    }
}
