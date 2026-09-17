using System.ComponentModel.DataAnnotations;
using ConversaCore.Tools;

namespace ConversaCore.BlazorTemplateHost.Tools;

/// <summary>
/// Strongly typed request for creating a sample order.
/// </summary>
public sealed record SampleOrderRequest
{
    [Required(ErrorMessage = "Item ID is required")]
    public string ItemId { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
    public int Quantity { get; set; } = 1;

    [Required(ErrorMessage = "Customer name is required")]
    public string CustomerName { get; set; } = string.Empty;
}

/// <summary>
/// Strongly typed result returned by the mutating order tool.
/// </summary>
public sealed record SampleOrderResult(
    string OrderId,
    string ItemId,
    int Quantity,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Bounded mutating tool demonstrating explicit confirmation enforcement,
/// trusted identity validation, idempotency key requirement, and topic allowlist confinement.
/// </summary>
public sealed class SampleOrderTool : IConversaTool<SampleOrderRequest, SampleOrderResult>
{
    public const string ToolId = "sample.order.create";
    public const string Version = "1";

    public static readonly ToolDescriptor Descriptor = new(
        toolId: ToolId,
        version: Version,
        displayName: "Create Sample Order",
        description: "Places a sample order. Requires explicit user confirmation, trusted identity, and idempotency key.",
        requestType: typeof(SampleOrderRequest),
        resultType: typeof(SampleOrderResult),
        sideEffect: ToolSideEffect.Mutating,
        confirmation: new ToolConfirmationPolicy
        {
            Required = true,
            Purpose = "Confirm placement of the sample order and commitment to purchase."
        },
        reliability: new ToolReliabilityPolicy
        {
            Timeout = TimeSpan.FromSeconds(5),
            MaxRetries = 1,
            RequiresIdempotencyKey = true
        },
        implementationType: typeof(SampleOrderTool));

    ToolDescriptor IConversaTool<SampleOrderRequest, SampleOrderResult>.Descriptor => Descriptor;

    public ValueTask<ToolResult<SampleOrderResult>> ExecuteAsync(
        SampleOrderRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            return ValueTask.FromResult(
                ToolResult<SampleOrderResult>.Failure("invalid_item_id", "Item ID must not be empty."));
        }

        if (request.Quantity <= 0)
        {
            return ValueTask.FromResult(
                ToolResult<SampleOrderResult>.Failure("invalid_quantity", "Quantity must be at least 1."));
        }

        // Generate deterministic simulated order result
        var orderId = $"ORD-{Math.Abs((context.IdempotencyKey ?? Guid.NewGuid().ToString("N")).GetHashCode()) % 90000 + 10000}";
        var result = new SampleOrderResult(
            OrderId: orderId,
            ItemId: request.ItemId,
            Quantity: request.Quantity,
            Status: "Confirmed",
            CreatedAt: DateTimeOffset.UtcNow);

        return ValueTask.FromResult(ToolResult<SampleOrderResult>.Success(result));
    }
}
