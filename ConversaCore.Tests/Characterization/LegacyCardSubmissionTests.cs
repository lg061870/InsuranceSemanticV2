using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ConversaCore.Events;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging.Abstractions;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Tests.Characterization;

public class LegacyCardSubmissionTests {
    [Fact]
    public async Task RequiredCard_RendersMetadataAndPausesFlowBeforeFollowingActivity() {
        var flow = new CardFlow();
        var card = new FormCard(flow.Context) { IsRequired = true };
        var after = new SimpleActivity("after", "complete");
        CardJsonEventArgs? sent = null;
        card.CardJsonSent += (_, e) => sent = e;
        flow.Add(card).Add(after);

        var result = await flow.RunAsync();

        Assert.True(result.RequiresInput);
        Assert.Equal(Flow.FlowState.WaitingForInput, flow.State);
        Assert.Same(card, flow.GetCurrentActivity());
        Assert.Equal(ActivityState.WaitingForUserInput, card.CurrentState);
        Assert.Equal(ActivityState.Created, after.CurrentState);
        Assert.NotNull(sent);
        Assert.True(sent.IsRequired);
        Assert.Equal("form", sent.CardId);
        using var json = JsonDocument.Parse(sent.CardJson);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        Assert.Contains("isRequired", sent.CardJson);
    }

    [Fact]
    public async Task ValidSubmission_BindsModel_UnlocksPromptAndResumesFollowingActivityOnce() {
        var flow = new CardFlow();
        var card = new FormCard(flow.Context) { IsRequired = true };
        var afterRuns = 0;
        var completions = 0;
        var outputs = new List<CardJsonEventArgs>();
        card.CardJsonSent += (_, e) => outputs.Add(e);
        card.ActivityCompleted += (_, _) => completions++;
        flow.Add(card).Add(SimpleActivity.Create("after", (TopicWorkflowContext _) => { afterRuns++; }));
        await flow.RunAsync();

        card.OnInputCollected(Submission("Ada"));

        Assert.Equal(ActivityState.Completed, card.CurrentState);
        Assert.Equal("Ada", flow.Context.GetValue<FormModel>("form_model").Name);
        Assert.False(outputs.Last().IsRequired);
        Assert.Equal(0, afterRuns); // Activity completion alone does not drive a bare TopicFlow.
        Assert.True((await flow.ResumeAsync("submitted")).IsCompleted);
        card.OnInputCollected(Submission("duplicate"));
        Assert.Equal(1, afterRuns);
        Assert.Equal(1, completions);
        Assert.Equal("Ada", flow.Context.GetValue<FormModel>("form_model").Name);
    }

    [Fact]
    public async Task InvalidSubmission_RerendersRequiredCard_ThenAcceptsCorrection() {
        var context = new TopicWorkflowContext();
        var card = new FormCard(context) { IsRequired = true };
        var invalid = 0;
        var completed = 0;
        var sent = new List<CardJsonEventArgs>();
        card.ValidationFailed += (_, _) => invalid++;
        card.ActivityCompleted += (_, _) => completed++;
        card.CardJsonSent += (_, e) => sent.Add(e);
        await card.RunAsync(context);

        card.OnInputCollected(Submission(""));

        Assert.Equal(1, invalid);
        Assert.Equal(0, completed);
        Assert.Equal(ActivityState.WaitingForUserInput, card.CurrentState);
        Assert.Null(context.GetValue<FormModel>("form_model"));
        Assert.True(sent.Last().IsRequired);
        Assert.Equal(2, sent.Count);

        card.OnInputCollected(Submission("corrected"));
        Assert.Equal(ActivityState.Completed, card.CurrentState);
        Assert.Equal(1, completed);
        Assert.Equal("corrected", context.GetValue<FormModel>("form_model").Name);
        Assert.False(sent.Last().IsRequired);
    }

    [Fact]
    public void SubmissionBeforeRender_IsIgnored() {
        var context = new TopicWorkflowContext();
        var card = new FormCard(context);

        card.OnInputCollected(Submission("too early"));

        Assert.Equal(ActivityState.Created, card.CurrentState);
        Assert.Null(context.GetValue<FormModel>("form_model"));
    }

    [Fact]
    public async Task DirectInputBypassesSubmissionProtocol_AndFailsActivity() {
        var context = new TopicWorkflowContext();
        var card = new FormCard(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => card.RunAsync(context, new { Name = "Ada" }));

        Assert.Equal(ActivityState.Failed, card.CurrentState);
    }

    private static AdaptiveCardInputCollectedEventArgs Submission(string name) =>
        new(new Dictionary<string, object> { ["Name"] = name });

    public sealed class FormModel {
        [Required]
        public string? Name { get; set; }
    }

    private sealed class CardFlow() : Flow(new TopicWorkflowContext(), NullLogger.Instance, "form-topic");
    private sealed class FormCard(TopicWorkflowContext context)
        : AdaptiveCardActivity<FormModel>("form", context, NullLogger<AdaptiveCardActivity<FormModel>>.Instance, "form_model") {
        protected override string GetCardJson(TopicWorkflowContext context) => """
            {"type":"AdaptiveCard","version":"1.5","body":[{"type":"Input.Text","id":"Name","label":"Name"}],"actions":[{"type":"Action.Submit","title":"Continue"}]}
            """;
    }
}
