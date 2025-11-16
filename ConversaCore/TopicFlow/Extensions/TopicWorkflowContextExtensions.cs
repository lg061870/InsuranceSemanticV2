using ConversaCore.TopicFlow;

namespace ConversaCore.TopicFlow.Extensions;

public static class TopicWorkflowContextExtensions {
    public static bool TryGetValue(this TopicWorkflowContext context, string key, out object? value) {
        value = null;

        var field = context.GetType().GetField("_values", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(context) is IDictionary<string, object?> dict) {
            return dict.TryGetValue(key, out value);
        }

        return false;
    }
}
