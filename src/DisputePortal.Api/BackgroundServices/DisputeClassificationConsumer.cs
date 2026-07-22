using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using DisputePortal.Api.Data;
using DisputePortal.Api.Domain;
using DisputePortal.Api.Messaging;
using DisputePortal.Api.Messaging.Events;
using DisputePortal.Api.Repositories;
using DisputePortal.Api.Services.Ai;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace DisputePortal.Api.BackgroundServices;

/// <summary>
/// Hosted service that consumes <c>dispute.submitted</c>, classifies each dispute via Claude
/// (TDP-AI-02), persists the category/priority, and publishes <c>dispute.classified</c> — all
/// inside the same API process (SPEC §3.1). Any AI failure marks the dispute
/// <c>CLASSIFICATION_FAILED</c> and is surfaced for manual triage without blocking submission
/// (AC-AI-02). Offsets are committed manually after handling (at-least-once) and the handler is
/// idempotent against re-delivery. The broker may not be ready at startup, so the connect/consume
/// loop retries with backoff rather than crashing the host.
/// </summary>
public sealed class DisputeClassificationConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    IOptions<GeminiOptions> geminiOptions,
    ILogger<DisputeClassificationConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Backoff = TimeSpan.FromSeconds(5);

    private readonly KafkaOptions _opts = options.Value;
    private readonly string _classificationModel = geminiOptions.Value.ClassificationModel;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.EnableClassificationConsumer)
        {
            logger.LogInformation("Dispute classification consumer is disabled by configuration.");
            return Task.CompletedTask;
        }

        // Kafka's Consume is a blocking call — run the loop on a dedicated long-running task so
        // we never tie up a thread-pool thread.
        return Task.Factory.StartNew(
            () => RunAsync(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _opts.BootstrapServers,
            GroupId = _opts.ConsumerGroupId,
            ClientId = $"{_opts.ClientId}-classifier",
            EnableAutoCommit = false, // commit only after successful handling (at-least-once)
            AutoOffsetReset = Enum.TryParse<AutoOffsetReset>(_opts.AutoOffsetReset, ignoreCase: true, out var r)
                ? r
                : AutoOffsetReset.Earliest
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<string, string>(config)
                    .SetErrorHandler((_, e) =>
                        logger.LogError("Kafka consumer error: {Reason} (fatal={Fatal})", e.Reason, e.IsFatal))
                    .Build();

                consumer.Subscribe(_opts.Topics.DisputeSubmitted);
                logger.LogInformation("Subscribed to {Topic} as group {GroupId}",
                    _opts.Topics.DisputeSubmitted, _opts.ConsumerGroupId);

                await ConsumeLoopAsync(consumer, stoppingToken);
                consumer.Close(); // graceful leave → clean rebalance on shutdown
                return;
            }
            catch (OperationCanceledException)
            {
                return; // host stopping
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Classification consumer connection failed (broker not ready?); retrying in {Backoff}s",
                    Backoff.TotalSeconds);
                try { await Task.Delay(Backoff, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task ConsumeLoopAsync(IConsumer<string, string> consumer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);
            if (result?.Message is null) continue;

            var correlationId = ReadCorrelationId(result.Message.Headers);
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await HandleAsync(scope.ServiceProvider, result, stoppingToken);
                }
                catch (Exception ex)
                {
                    // A handler that throws unexpectedly must not wedge the partition; log and move on.
                    logger.LogError(ex,
                        "Unhandled error classifying message at {Topic}[{Partition}]@{Offset}",
                        result.Topic, result.Partition.Value, result.Offset.Value);
                }

                // Always commit — success, CLASSIFICATION_FAILED, or a poison message — so the
                // consumer never loops forever on one record (TDP-AI-02 §2.7).
                consumer.Commit(result);
            }
        }
    }

    private async Task HandleAsync(IServiceProvider sp, ConsumeResult<string, string> cr, CancellationToken ct)
    {
        logger.LogInformation("Consumed {Topic}[{Partition}]@{Offset}",
            cr.Topic, cr.Partition.Value, cr.Offset.Value);

        SubmittedEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SubmittedEnvelope>(cr.Message.Value, Json);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "dispute.submitted payload was not valid JSON — skipping (poison).");
            return;
        }

        if (envelope is null || envelope.DisputeId == Guid.Empty)
        {
            logger.LogError("dispute.submitted payload had no disputeId — skipping.");
            return;
        }

        var db = sp.GetRequiredService<DisputePortalDbContext>();
        var repository = sp.GetRequiredService<IDisputeRepository>();
        var classifier = sp.GetRequiredService<IDisputeClassificationService>();
        var publisher = sp.GetRequiredService<IEventPublisher>();

        var dispute = await repository.GetTrackedForClassificationAsync(envelope.DisputeId, ct);
        if (dispute is null)
        {
            logger.LogWarning("Dispute {DisputeId} not found — nothing to classify.", envelope.DisputeId);
            return;
        }

        // Idempotency against re-delivery: skip if already classified.
        if ((dispute.Category is not null && dispute.Priority is not null)
            || dispute.Events.Any(e => e.EventType == DisputeEventType.CLASSIFIED))
        {
            logger.LogInformation("Dispute {Reference} already classified — skipping (idempotent).", dispute.Reference);
            return;
        }

        var openCount = await repository.CountOpenForCustomerAsync(dispute.CustomerId, dispute.Id, ct);
        var context = new ClassificationContext(
            dispute.Transaction.MerchantName,
            dispute.Transaction.MerchantCategory,
            dispute.Transaction.Amount,
            dispute.Transaction.TransactionDate,
            dispute.CustomerDescription,
            openCount);

        var result = await classifier.ClassifyAsync(context, ct);
        var now = DateTimeOffset.UtcNow;

        if (!result.Success)
        {
            dispute.Status = DisputeStatus.CLASSIFICATION_FAILED;
            dispute.UpdatedAt = now;
            db.DisputeEvents.Add(new DisputeEvent
            {
                Id = Guid.NewGuid(),
                DisputeId = dispute.Id,
                EventType = DisputeEventType.CLASSIFIED, // timeline entry; description records the failure
                ActorId = null,                          // system event
                Description = $"Automatic classification failed ({result.FailureReason}); flagged for manual triage.",
                CreatedAt = now
            });
            await db.SaveChangesAsync(ct);

            logger.LogWarning("Dispute {Reference} flagged CLASSIFICATION_FAILED: {Reason}",
                dispute.Reference, result.FailureReason);
            return; // do NOT publish dispute.classified
        }

        dispute.Category = result.Category;
        dispute.Priority = result.Priority;
        dispute.UpdatedAt = now;
        db.DisputeEvents.Add(new DisputeEvent
        {
            Id = Guid.NewGuid(),
            DisputeId = dispute.Id,
            EventType = DisputeEventType.CLASSIFIED,
            ActorId = null,
            Description = $"Classified as {result.Category}/{result.Priority}."
                          + (string.IsNullOrWhiteSpace(result.Rationale) ? "" : $" {result.Rationale}"),
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);

        await publisher.PublishAsync(new DisputeClassifiedEvent(
            dispute.Id,
            dispute.Reference,
            result.Category!.Value.ToString(),
            result.Priority!.Value.ToString(),
            ClassifiedBy: _classificationModel), ct);

        logger.LogInformation("Dispute {Reference} classified as {Category}/{Priority}",
            dispute.Reference, result.Category, result.Priority);
    }

    private static string ReadCorrelationId(Headers headers)
    {
        if (headers is not null && headers.TryGetLastBytes("correlationId", out var bytes))
            return Encoding.UTF8.GetString(bytes);
        return Guid.NewGuid().ToString("n");
    }

    // Consumed shape of dispute.submitted (SPEC §3.4). We only need disputeId; the write path
    // reloads the authoritative dispute from the DB rather than trusting denormalised fields.
    private sealed record SubmittedEnvelope(
        [property: JsonPropertyName("disputeId")] Guid DisputeId,
        [property: JsonPropertyName("reference")] string? Reference);
}
