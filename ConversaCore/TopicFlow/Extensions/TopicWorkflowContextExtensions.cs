using System.Text.Json;

namespace ConversaCore.TopicFlow;

public static class TopicWorkflowContextExtensions {
    public static bool TryGetValue(this TopicWorkflowContext context, string key, out object? value) {
        value = null;

        var field = context.GetType().GetField("_values", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(context) is IDictionary<string, object?> dict) {
            return dict.TryGetValue(key, out value);
        }

        return false;
    }
    /// <summary>
    /// Gets a string value from context with multiple key fallbacks
    /// </summary>
    public static string GetString(this TopicWorkflowContext context, params string[] keys) {
        if (context == null || keys == null || !keys.Any())
            return "n/a";

        var contextKeys = context.GetKeys().ToList();

        foreach (var candidateKey in keys.Where(k => !string.IsNullOrWhiteSpace(k))) {
            // Try exact match (case-insensitive)
            var exactMatch = contextKeys.FirstOrDefault(k =>
                string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null) {
                var value = context.GetValue<object>(exactMatch);
                var formatted = FormatValue(value);
                if (!string.IsNullOrWhiteSpace(formatted) && formatted != "—")
                    return formatted;
            }

            // Try partial match (contains)
            var partialMatch = contextKeys.FirstOrDefault(k =>
                k.IndexOf(candidateKey, StringComparison.OrdinalIgnoreCase) >= 0);

            if (partialMatch != null) {
                var value = context.GetValue<object>(partialMatch);
                var formatted = FormatValue(value);
                if (!string.IsNullOrWhiteSpace(formatted) && formatted != "—")
                    return formatted;
            }
        }

        return "n/a";
    }

    /// <summary>
    /// Gets an integer value from context with fallbacks
    /// </summary>
    public static int GetInt(this TopicWorkflowContext context, int defaultValue, params string[] keys) {
        var stringValue = context.GetString(keys);
        if (int.TryParse(stringValue, out var result))
            return result;
        return defaultValue;
    }

    /// <summary>
    /// Gets a boolean value from context with fallbacks
    /// </summary>
    public static bool GetBool(this TopicWorkflowContext context, params string[] keys) {
        var stringValue = context.GetString(keys);
        if (bool.TryParse(stringValue, out var result))
            return result;

        // Handle common string representations
        return stringValue.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               stringValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               stringValue == "1";
    }

    /// <summary>
    /// Gets a formatted age string from DOB context keys
    /// </summary>
    public static string GetAge(this TopicWorkflowContext context, params string[] dobKeys) {
        if (context == null)
            return "n/a";

        var contextKeys = context.GetKeys().ToList();

        foreach (var dobKey in dobKeys.Where(k => !string.IsNullOrWhiteSpace(k))) {
            var match = contextKeys.FirstOrDefault(k =>
                string.Equals(k, dobKey, StringComparison.OrdinalIgnoreCase)) ??
                contextKeys.FirstOrDefault(k =>
                    k.IndexOf(dobKey, StringComparison.OrdinalIgnoreCase) >= 0);

            if (match == null) continue;

            var value = context.GetValue<object>(match);
            if (value == null) continue;

            // Handle DateTime
            if (value is DateTime dt) {
                var years = CalculateAge(dt, DateTime.Now);
                return years >= 0 ? $"{years} years" : "n/a";
            }

            // Handle DateTimeOffset
            if (value is DateTimeOffset dto) {
                var years = CalculateAge(dto.UtcDateTime, DateTime.Now);
                return years >= 0 ? $"{years} years" : "n/a";
            }

            // Handle JsonElement
            if (value is JsonElement je && je.ValueKind == JsonValueKind.String) {
                var dateString = je.GetString();
                if (DateTime.TryParse(dateString, out var parsed)) {
                    var years = CalculateAge(parsed, DateTime.Now);
                    return years >= 0 ? $"{years} years" : "n/a";
                }
            }

            // Handle string
            var stringValue = value.ToString();
            if (!string.IsNullOrWhiteSpace(stringValue) &&
                DateTime.TryParse(stringValue, out var parsed2)) {
                var years = CalculateAge(parsed2, DateTime.Now);
                return years >= 0 ? $"{years} years" : "n/a";
            }
        }

        // Fallback: check for direct "age" keys
        var ageValue = context.GetString("customer_age", "age", "lead_age");
        if (!string.IsNullOrWhiteSpace(ageValue) && ageValue != "n/a") {
            // If it's already a number, format it
            if (int.TryParse(ageValue, out var ageInt))
                return $"{ageInt} years";
            return ageValue;
        }

        return "n/a";
    }

    /// <summary>
    /// Gets a percentage value (0-100) from context
    /// </summary>
    public static int GetPercent(this TopicWorkflowContext context, params string[] keys) {
        var stringValue = context.GetString(keys);

        if (int.TryParse(stringValue, out var intValue))
            return Math.Clamp(intValue, 0, 100);

        if (double.TryParse(stringValue, out var doubleValue))
            return (int)Math.Clamp(Math.Round(doubleValue), 0, 100);

        return 0;
    }

    /// <summary>
    /// Calculates age from birthdate
    /// </summary>
    private static int CalculateAge(DateTime birthDate, DateTime now) {
        int age = now.Year - birthDate.Year;
        if (now.Date < birthDate.Date.AddYears(age))
            age--;
        return age;
    }

    /// <summary>
    /// Formats various object types to display strings
    /// </summary>
    private static string FormatValue(object? value) {
        return value switch {
            null => "—",
            string s when string.IsNullOrWhiteSpace(s) => "—",
            string s => s,
            DateTime dt => dt.ToString("MMM d, yyyy"),
            DateTimeOffset dto => dto.ToString("MMM d, yyyy"),
            bool b => b ? "Yes" : "No",
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? "—",
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetRawText(),
            JsonElement je when je.ValueKind == JsonValueKind.True => "Yes",
            JsonElement je when je.ValueKind == JsonValueKind.False => "No",
            JsonElement je when je.ValueKind == JsonValueKind.Array =>
                string.Join(", ", je.EnumerateArray().Select(e => e.GetString())),
            System.Collections.IEnumerable enumerable when enumerable is not string =>
                string.Join(", ", enumerable.Cast<object>().Select(o => o?.ToString() ?? "")),
            _ => value.ToString() ?? "—"
        };
    }
}
