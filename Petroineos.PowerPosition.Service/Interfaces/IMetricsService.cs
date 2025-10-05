namespace Petroineos.PowerPosition.Service.Interfaces
{
    public interface IMetricsService
    {
        void RecordSuccessfulRun(int tradesProcessed = 0);
        void RecordFailedRun();
        void LogMetricsSummary();
    }
}
