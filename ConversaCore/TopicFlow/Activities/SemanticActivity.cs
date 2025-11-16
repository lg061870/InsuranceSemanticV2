using ConversaCore.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Diagnostics;
using System.Text.Json;

namespace ConversaCore.TopicFlow;

/// <summary>
/// Base class for semantic (LLM-driven) activities compatible with SK 1.66.
/// Provides background execution, async completion callbacks, and event signaling.
/// </summary>
public abstract class SemanticActivity : TopicFlowActivity, IAsyncNotifiableActivity {
    private static readonly ActivitySource _otelSource = new("ConversaCore.Semantic");

    protected readonly Kernel _kernel;
    protected readonly IChatCompletionService _chatCompletion;
    protected readonly ILogger _semanticLogger;

    private string? _cachedSystemPrompt;

    public string ModelId { get; set; }
    public float Temperature { get; set; }
    public int MaxTokens { get; set; }
    public bool RequireJsonOutput { get; set; }
    public string? JsonSchemaHint { get; set; }

    /// <summary> Indicates whether the activity should execute asynchronously in background. </summary>
    public bool RunInBackground { get; set; }

    /// <summary> User-supplied callback to execute when async reasoning completes. </summary>
    public Func<TopicWorkflowContext, Task<TopicFlowActivity?>>? OnAsyncCompletedCallback { get; private set; }

    /// <summary> Raised when the semantic query completes asynchronously. </summary>
    public event EventHandler<AsyncQueryCompletedEventArgs>? AsyncCompleted;

    protected SemanticActivity(
        string activityId,
        Kernel kernel,
        ILogger logger,
        IOptions<SemanticActivityOptions>? options = null)
        : base(activityId, null) {

        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _semanticLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        var cfg = options?.Value ?? new SemanticActivityOptions();
        ModelId = cfg.DefaultModelId;
        Temperature = cfg.DefaultTemperature;
        MaxTokens = cfg.DefaultMaxTokens;
        RequireJsonOutput = cfg.RequireJsonOutput;
    }

    // ============================================================
    // CALLBACK REGISTRATION
    // ============================================================
    public void OnAsyncCompleted(Func<TopicWorkflowContext, Task<TopicFlowActivity?>> callback) {
        OnAsyncCompletedCallback = async ctx =>
        {
            // Execute user-defined callback (e.g., AttachQueryPayloadAsync)
            var activity = await callback(ctx);

            // ✅ Build event args and raise AsyncCompleted
            if (AsyncCompleted != null) {
                try {
                    var args = new AsyncQueryCompletedEventArgs(
                        ctx,
                        output: null,        // no specific payload — semantic result already in context
                        queryId: Id,
                        activity: activity   // attach the created activity
                    );

                    AsyncCompleted.Invoke(this, args);
                } catch (Exception ex) {
                    _semanticLogger.LogWarning(
                        ex,
                        "[{ActivityId}] ⚠ Failed to raise AsyncCompleted event after callback.",
                        Id
                    );

                    return null;
                }
            }

            return null;
        };
    }

    protected void RaiseAsyncCompleted(TopicWorkflowContext context, TopicFlowActivity nextActivity) {
        try {
            var queryId = nextActivity.Id;
            var output = context.GetValue<object>($"{Id}_Result");

            _semanticLogger.LogInformation(
                "[{ActivityId}] 📡 Raising AsyncCompleted event for next activity {NextActivityId}",
                Id, queryId);

            AsyncCompleted?.Invoke(this, new AsyncQueryCompletedEventArgs(context, output, queryId));
        } catch (Exception ex) {
            _semanticLogger.LogWarning(ex,
                "[{ActivityId}] ⚠ Failed to raise AsyncCompleted event.", Id);
        }
    }

    // ============================================================
    // EXECUTION ENTRY POINT
    // ============================================================
    protected override async Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default) {

        if (RunInBackground) {
            _semanticLogger.LogInformation("[{ActivityId}] 🚀 Running semantic activity in background.", Id);

            _ = Task.Run(async () => {
                try {
                    var result = await RunSemanticCoreAsync(context, input, cancellationToken);

                    // 🔹 Run user-provided callback (if any)
                    TopicFlowActivity? nextActivity = null;
                    if (OnAsyncCompletedCallback != null) {
                        try {
                            _semanticLogger.LogDebug("[{ActivityId}] 🔁 Executing OnAsyncCompleted callback...", Id);
                            nextActivity = await OnAsyncCompletedCallback(context);
                        } catch (Exception cbEx) {
                            _semanticLogger.LogWarning(cbEx,
                                "[{ActivityId}] ⚠ OnAsyncCompleted callback failed.", Id);
                        }
                    }

                    // 🔹 Raise AsyncCompleted event
                    if (nextActivity != null)
                        RaiseAsyncCompleted(context, nextActivity);

                    _semanticLogger.LogInformation("[{ActivityId}] ✅ Background semantic activity completed.", Id);
                } catch (Exception ex) {
                    _semanticLogger.LogError(ex, "[{ActivityId}] ❌ Background semantic activity failed.", Id);
                }
            }, cancellationToken);

            return ActivityResult.Continue($"[{Id}] running in background");
        }

        // Default synchronous path
        return await RunSemanticCoreAsync(context, input, cancellationToken);
    }

    // ============================================================
    // CORE SEMANTIC EXECUTION LOGIC
    // ============================================================
    private async Task<ActivityResult> RunSemanticCoreAsync(
        TopicWorkflowContext context,
        object? input,
        CancellationToken cancellationToken) {

        using var otelActivity = _otelSource.StartActivity("SemanticActivity.Run", ActivityKind.Internal);
        otelActivity?.SetTag("activity.id", Id);
        otelActivity?.SetTag("model.id", ModelId);
        otelActivity?.SetTag("temperature", Temperature);
        otelActivity?.SetTag("require.json", RequireJsonOutput);

        var stopwatch = Stopwatch.StartNew();

        try {
            _semanticLogger.LogInformation("Executing semantic activity {ActivityId}", Id);

            // Build prompts
            var systemPrompt = _cachedSystemPrompt ??= await BuildSystemPromptAsync(context, input);
            var userPrompt = await BuildUserPromptAsync(context, input);
            var fullPrompt = $"{systemPrompt}\n\nUser: {userPrompt}";

            var exec = new OpenAIPromptExecutionSettings {
                ModelId = ModelId,
                Temperature = Temperature,
                MaxTokens = MaxTokens
            };

            if (RequireJsonOutput) {
                exec.ResponseFormat = "json_object";
                fullPrompt += "\n\nRespond with valid JSON only. No text outside the object.";
            }

            _semanticLogger.LogDebug("[{ActivityId}] Sending prompt to {ModelId}", Id, ModelId);

            // Run the OpenAI call
            var response = await _chatCompletion.GetChatMessageContentAsync(
                fullPrompt,
                exec,
                cancellationToken: cancellationToken);

            var text = response.Content ?? string.Empty;
            stopwatch.Stop();

            var tokens = response.Metadata?.TryGetValue("token_count", out var tk) == true
                ? Convert.ToInt32(tk)
                : 0;

            otelActivity?.SetTag("duration.ms", stopwatch.Elapsed.TotalMilliseconds);
            otelActivity?.SetTag("tokens.used", tokens);
            otelActivity?.SetTag("response.length", text.Length);

            _semanticLogger.LogInformation(
                "[{ActivityId}] Model={ModelId}, Tokens={Tokens}, Duration={Duration} ms",
                Id, ModelId, tokens, stopwatch.ElapsedMilliseconds);

            var processed = await ProcessResponseAsync(context, input, text);
            processed = await OnResponseReadyAsync(context, processed);
            await StoreResultsInContextAsync(context, processed);

            return ActivityResult.Continue(processed, processed);
        } catch (OperationCanceledException) {
            stopwatch.Stop();
            _semanticLogger.LogWarning("[{ActivityId}] Canceled", Id);
            return ActivityResult.Continue("Operation canceled.");
        } catch (JsonException ex) {
            stopwatch.Stop();
            _semanticLogger.LogWarning(ex, "[{ActivityId}] Invalid JSON", Id);
            return await HandleErrorAsync(context, input, ex, "Invalid JSON format received.");
        } catch (Exception ex) {
            stopwatch.Stop();
            _semanticLogger.LogError(ex, "[{ActivityId}] Error executing semantic activity", Id);
            return await HandleErrorAsync(context, input, ex);
        }
    }

    // ============================================================
    // PROMPT BUILDERS
    // ============================================================
    protected abstract Task<string> BuildSystemPromptAsync(TopicWorkflowContext context, object? input);
    protected abstract Task<string> BuildUserPromptAsync(TopicWorkflowContext context, object? input);

    // ============================================================
    // RESPONSE PROCESSING / CONTEXT STORAGE
    // ============================================================
    protected virtual async Task<string> ProcessResponseAsync(
        TopicWorkflowContext context, object? input, string response) {
        await Task.CompletedTask;

        if (RequireJsonOutput) {
            try { JsonDocument.Parse(response); } catch (JsonException) {
                _semanticLogger.LogWarning("[{ActivityId}] Attempting JSON cleanup", Id);
                response = ExtractJsonFromResponse(response);
            }
        }
        return response;
    }

    protected virtual async Task<string> OnResponseReadyAsync(
        TopicWorkflowContext context, string response) {
        await Task.CompletedTask;
        return response.Trim();
    }

    protected async Task StoreResultsInContextAsync(
        TopicWorkflowContext context, string response) {
        await Task.CompletedTask;
        var key = $"{Id}_Result";
        context.SetValue(key, response);

        if (RequireJsonOutput) {
            try {
                var jsonDoc = JsonDocument.Parse(response);
                context.SetValue($"{key}_Json", jsonDoc);
            } catch (JsonException ex) {
                _semanticLogger.LogWarning(ex,
                    "[{ActivityId}] JSON parse failed during storage", Id);
            }
        }

        _semanticLogger.LogDebug("[{ActivityId}] Stored result in context (Key={Key})", Id, key);
    }

    protected virtual async Task<ActivityResult> HandleErrorAsync(
        TopicWorkflowContext context, object? input, Exception ex, string? userMessage = null) {
        await Task.CompletedTask;
        var msg = userMessage ?? "Sorry, an error occurred while processing your request.";
        context.SetValue($"{Id}_Error", ex.Message);
        return ActivityResult.Continue(msg);
    }

    // ============================================================
    // UTILITIES
    // ============================================================
    private static string ExtractJsonFromResponse(string text) {
        try {
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start) return text[start..(end + 1)];
            start = text.IndexOf('[');
            end = text.LastIndexOf(']');
            if (start >= 0 && end > start) return text[start..(end + 1)];
        } catch { }
        return text;
    }
}

/// <summary>
/// Default configuration options for SemanticActivity.
/// </summary>
public class SemanticActivityOptions {
    public string DefaultModelId { get; set; } = "gpt-4o-mini";
    public float DefaultTemperature { get; set; } = 0.7f;
    public int DefaultMaxTokens { get; set; } = 2048;
    public bool RequireJsonOutput { get; set; } = false;
}
