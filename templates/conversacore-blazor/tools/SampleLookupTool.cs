using System.ComponentModel.DataAnnotations;
using ConversaCore.Tools;

namespace ConversaCore.BlazorTemplateHost.Tools;

/// <summary>
/// Strongly typed request for looking up sample catalog item information.
/// </summary>
public sealed record SampleLookupRequest
{
    [Required(ErrorMessage = "Item ID is required")]
    public string ItemId { get; set; } = string.Empty;
}

/// <summary>
/// Strongly typed result returned by the read-only lookup tool.
/// </summary>
public sealed record SampleLookupResult(
    string ItemId,
    string Name,
    string Category,
    decimal Price,
    bool InStock);

/// <summary>
/// Bounded read-only tool demonstrating typed request/result contracts,
/// deterministic lookup, and safe execution within topic allowlists.
/// </summary>
public sealed class SampleLookupTool : IConversaTool<SampleLookupRequest, SampleLookupResult>
{
    public const string ToolId = "sample.item.lookup";
    public const string Version = "1";

    public static readonly ToolDescriptor Descriptor = new(
        toolId: ToolId,
        version: Version,
        displayName: "Lookup Sample Item",
        description: "Retrieves status, category, and pricing for a sample catalog item.",
        requestType: typeof(SampleLookupRequest),
        resultType: typeof(SampleLookupResult),
        sideEffect: ToolSideEffect.ReadOnly,
        confirmation: new ToolConfirmationPolicy { Required = false },
        reliability: new ToolReliabilityPolicy { Timeout = TimeSpan.FromSeconds(5), MaxRetries = 1 },
        implementationType: typeof(SampleLookupTool));

    ToolDescriptor IConversaTool<SampleLookupRequest, SampleLookupResult>.Descriptor => Descriptor;

    public ValueTask<ToolResult<SampleLookupResult>> ExecuteAsync(
        SampleLookupRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            return ValueTask.FromResult(
                ToolResult<SampleLookupResult>.Failure("invalid_item_id", "Item ID must not be empty."));
        }

        var normalizedId = request.ItemId.Trim().ToUpperInvariant();

        // Sample in-memory catalog for demonstration
        var item = normalizedId switch
        {
            "ITEM-101" or "101" => new SampleLookupResult("ITEM-101", "Standard Widget", "Hardware", 24.99m, true),
            "ITEM-102" or "102" => new SampleLookupResult("ITEM-102", "Pro Sensor", "Electronics", 89.50m, true),
            "ITEM-103" or "103" => new SampleLookupResult("ITEM-103", "Heavy Duty Bracket", "Hardware", 14.25m, false),
            _ => new SampleLookupResult(normalizedId, $"Sample Item ({normalizedId})", "General", 49.99m, true)
        };

        return ValueTask.FromResult(ToolResult<SampleLookupResult>.Success(item));
    }
}
