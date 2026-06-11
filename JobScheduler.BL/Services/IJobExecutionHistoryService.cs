using JobScheduler.BL.Contracts;

namespace JobScheduler.BL.Services;

/// <summary>UC 2.3 — read execution history for a job (DynamoDB <c>JobExecutionsIndex</c>).</summary>
public interface IJobExecutionHistoryService
{
    /// <param name="fullDetails">When false, omits large fields (stack traces, parameter maps, long error text).</param>
    Task<ExecutionHistoryPageJson> GetExecutionHistoryPageAsync(
        Guid jobId,
        int limit,
        string? paginationToken,
        bool fullDetails,
        CancellationToken cancellationToken = default);
}
