using System.Net.Http.Json;
using System.Text.Json;
using JobScheduler.DAL.DynamoDB.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.JobOrchestrator.Workers;

/// <summary>
/// UC 2.1 batch-style path: periodically polls the API for pending execution rows (micro-batch observability / coordinator view).
/// Does not claim work — workers would use DynamoDB <c>TryClaimAsync</c> in a full design.
/// </summary>
public sealed class PendingExecutionBatchPeekWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<OrchestratorBatchOptions> _options;
    private readonly ILogger<PendingExecutionBatchPeekWorker> _logger;

    public PendingExecutionBatchPeekWorker(
        IHttpClientFactory httpClientFactory,
        IOptions<OrchestratorBatchOptions> options,
        ILogger<PendingExecutionBatchPeekWorker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = _options.Value;
        _logger.LogInformation(
            "Pending execution batch peek starting interval={Interval}s api={Api} limit={Limit}",
            o.IntervalSeconds,
            o.ApiBaseUrl,
            o.PendingPeekLimit);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(10, o.IntervalSeconds)));

        await PeekOnceAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await PeekOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Batch peek failed");
            }
        }
    }

    private async Task PeekOnceAsync(CancellationToken cancellationToken)
    {
        var o = _options.Value;
        var client = _httpClientFactory.CreateClient("SchedulerApi");
        var limit = o.PendingPeekLimit;
        var uri = $"api/internal/execution/queue/pending?limit={limit}";
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Batch peek HTTP {StatusCode}", (int)response.StatusCode);
            return;
        }

        var items = await response.Content.ReadFromJsonAsync<List<ExecutionQueueItem>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken)
            .ConfigureAwait(false);

        var count = items?.Count ?? 0;
        _logger.LogInformation("Batch peek completed pendingCount={Count}", count);

        if (items is null || count == 0)
            return;

        foreach (var item in items.Take(5))
        {
            _logger.LogDebug(
                "Peeked pending queueId={QueueId} scheduledFor={ScheduledFor} jobId={JobId}",
                item.QueueId,
                item.ScheduledFor,
                item.JobId);
        }
    }
}
