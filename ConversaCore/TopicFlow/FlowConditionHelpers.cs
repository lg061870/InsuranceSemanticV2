using System;
using System.Threading.Tasks;
using ConversaCore.TopicFlow.Core;

namespace ConversaCore.TopicFlow;

public static class FlowConditionHelpers
{
    public static TopicFlowActivity IfCase(
        string id,
        Func<TopicWorkflowContext, bool> condition,
        TopicFlowActivity activity)
    {
        return ConditionalActivity<TopicFlowActivity>.If(
            id,
            condition,
            (yesId, ctx) => activity,
            (noId, ctx) => Skip(id));
    }

    public static TopicFlowActivity Skip(string id)
        => new SimpleActivity($"{id}_SKIP", (c, d) => Task.FromResult<object?>(null));
}
