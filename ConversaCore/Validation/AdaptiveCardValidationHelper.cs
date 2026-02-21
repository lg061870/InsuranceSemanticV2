using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class AdaptiveCardValidationHelper {
    /// <summary>
    /// Maps C# property names to JSON property names using JsonPropertyName attributes.
    /// Falls back to property name if no JsonPropertyName attribute is found.
    /// </summary>
    private static string GetJsonPropertyName(Type modelType, string propertyName) {
        var property = modelType.GetProperty(propertyName);
        if (property == null) return propertyName.ToLowerInvariant();
        
        var jsonAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        return jsonAttribute?.Name?.ToLowerInvariant() ?? propertyName.ToLowerInvariant();
    }

    public static string InjectErrors(string originalCardJson, List<ValidationResult> results, Dictionary<string, object>? userInputData = null, Type? modelType = null) {
        if (results == null || results.Count == 0)
            return originalCardJson; // <-- don't touch on initial render

        var root = JsonSerializer.Deserialize<Dictionary<string, object>>(originalCardJson);
        if (root == null || !root.ContainsKey("body"))
            return originalCardJson;

        // Deserialize body as raw list
        var bodyElements = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
            JsonSerializer.Serialize(root["body"])
        );

        if (bodyElements == null)
            return originalCardJson;

        // Group errors - map property names to JSON property names for reliable matching
        var errors = new Dictionary<string, List<string>>();
        
        foreach (var result in results) {
            foreach (var memberName in result.MemberNames) {
                string jsonFieldName;
                if (modelType != null) {
                    // Use JSON property name if model type is available
                    jsonFieldName = GetJsonPropertyName(modelType, memberName);
                } else {
                    // Fallback to lowercase property name
                    jsonFieldName = memberName.ToLowerInvariant();
                }
                
                // DEBUG: Log the mapping
                // Console.WriteLine($"[DEBUG] Validation field mapping: '{memberName}' -> '{jsonFieldName}'");
                
                if (!errors.ContainsKey(jsonFieldName)) {
                    errors[jsonFieldName] = new List<string>();
                }
                // Add just the error message string, not an object
                errors[jsonFieldName].Add(result.ErrorMessage ?? "Invalid value");
            }
        }

        // ✅ Convert errors to validationErrors object for JS renderer upfront
        // JS expects { fieldId: "error message" } format
        var validationErrors = new Dictionary<string, string>();
        foreach (var kvp in errors) {
            // Take first error message for each field (or join multiple with "; ")
            validationErrors[kvp.Key] = string.Join("; ", kvp.Value);
        }

        // Track which error field IDs we actually matched to card elements
        var matchedErrorIds = new HashSet<string>();

        var newBody = new List<object>();

        foreach (var element in bodyElements) {
            // Inject user input values before adding to newBody
            if (element.TryGetValue("id", out var idObj)) {
                var id = idObj?.ToString();
                if (!string.IsNullOrEmpty(id) && userInputData != null && userInputData.ContainsKey(id)) {
                    // Preserve user's input based on element type
                    var inputType = element.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : "";
                    var userValue = userInputData[id];
                    
                    switch (inputType) {
                        case "Input.Text":
                        case "Input.Number":
                        case "Input.Date":
                            element["value"] = userValue?.ToString() ?? "";
                            break;
                        case "Input.ChoiceSet":
                            if (userValue != null) {
                                element["value"] = userValue.ToString() ?? "";
                            }
                            break;
                        case "Input.Toggle":
                            if (userValue is bool boolValue) {
                                element["value"] = boolValue;
                            } else if (bool.TryParse(userValue?.ToString(), out var parsedBool)) {
                                element["value"] = parsedBool;
                            }
                            break;
                    }
                }
            }
            
            // First, clear any existing error styling from previous validation attempts
            if (element.ContainsKey("style") && element["style"]?.ToString() == "error") {
                element.Remove("style");
            }
            if (element.ContainsKey("errorStyle")) {
                element.Remove("errorStyle");
            }
            
            // Check for validation errors and apply styling BEFORE adding to body
            if (element.TryGetValue("id", out var errorIdObj)) {
                var errorId = errorIdObj?.ToString()?.ToLowerInvariant();
                // Console.WriteLine($"[DEBUG] Checking card element ID: '{errorId}'");
                if (!string.IsNullOrEmpty(errorId) && errors.ContainsKey(errorId)) {
                    // Track that we found this error field in the card
                    matchedErrorIds.Add(errorId);
                    
                    // For ChoiceSet elements, we need to preserve the "expanded" style
                    if (element.TryGetValue("type", out var typeObj) && 
                        typeObj?.ToString() == "Input.ChoiceSet" &&
                        element.TryGetValue("style", out var styleObj) && 
                        styleObj?.ToString() == "expanded") {
                        // Keep the "expanded" style and set an error CSS class instead
                        element["errorStyle"] = "error"; // Custom property for CSS targeting
                    } else {
                        // For other elements, use the standard error style
                        element["style"] = "error";
                    }
                }
            }
            
            // Always add element (now with preserved user input AND error styling)
            newBody.Add(element);
        }

        // Check if any validation errors could not be matched to card elements
        var unmatchedErrors = errors.Keys.Except(matchedErrorIds).ToList();
        if (unmatchedErrors.Any()) {
            var unmatched = string.Join(", ",
                unmatchedErrors.Select(id => $"{id}: {string.Join(" | ", errors[id])}"));
            throw new InvalidOperationException(
                $"Validation produced errors for fields not found in AdaptiveCard JSON: {unmatched}. " +
                $"Make sure property names match card element ids (case-insensitive).");
        }

        root["body"] = newBody;
        
        // ✅ Add validationErrors to root for JS renderer
        if (validationErrors.Any()) {
            root["validationErrors"] = validationErrors;
        }
        
        return JsonSerializer.Serialize(root);
    }

    /// <summary>
    /// Creates a "success" version of the card with user data preserved,
    /// all inputs disabled, and actions marked as completed/disabled.
    /// </summary>
    public static string InjectSuccessState(string originalCardJson, Dictionary<string, object> userInputData) {
        var root = JsonSerializer.Deserialize<Dictionary<string, object>>(originalCardJson);
        if (root == null || !root.ContainsKey("body"))
            return originalCardJson;

        // Deserialize body as raw list
        var bodyElements = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
            JsonSerializer.Serialize(root["body"])
        );

        if (bodyElements == null)
            return originalCardJson;

        var newBody = new List<object>();

        foreach (var element in bodyElements) {
            // Note: No need to skip error TextBlocks since we're now rendering errors via JS validationErrors object

            // Preserve user input and disable all input elements
            if (element.TryGetValue("id", out var idObj)) {
                var id = idObj?.ToString();
                if (!string.IsNullOrEmpty(id)) {
                    // Check if this element has user input data
                    if (userInputData.ContainsKey(id)) {
                        var inputType = element.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : "";
                        var userValue = userInputData[id];

                        switch (inputType) {
                            case "Input.Text":
                            case "Input.Number":
                            case "Input.Date":
                            case "Input.TagSelect": // ✅ support tag/select inputs (custom)
                                element["value"] = userValue?.ToString() ?? string.Empty;
                                element["isEnabled"] = false; // disable standard text-like inputs
                                break;

                            case "Input.ChoiceSet":
                                if (userValue != null)
                                    element["value"] = userValue.ToString() ?? string.Empty;
                                element["isEnabled"] = false; // disable choice set
                                break;

                            case "Input.Toggle":
                                if (userValue is bool boolValue) {
                                    element["value"] = boolValue;
                                }
                                else if (bool.TryParse(userValue?.ToString(), out var parsedBool)) {
                                    element["value"] = parsedBool;
                                }
                                else {
                                    // fallback to false for malformed values
                                    element["value"] = false;
                                }
                                element["isEnabled"] = false; // disable toggle
                                break;

                            default:
                                // 🔹 Unknown input types: preserve existing value if present, just disable them
                                if (element.ContainsKey("value")) {
                                    // Keep current value so card doesn’t blank out
                                    element["isEnabled"] = false;
                                }
                                break;
                        }
                    }
                    else {
                        // Element not in user input data - handle toggles that were not selected
                        var inputType = element.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : "";
                        if (inputType == "Input.Toggle") {
                            // For unselected toggles, ensure they show as unchecked
                            element["value"] = false;
                            element["isEnabled"] = false;
                        } else {
                            // For other input types, just disable them with current value
                            element["isEnabled"] = false;
                        }
                    }
                }
            }

            // Remove any error styling
            if (element.ContainsKey("style") && element["style"]?.ToString() == "error") {
                element.Remove("style");
            }
            
            // Remove errorStyle for choice sets (used to preserve expanded style while showing errors)
            if (element.ContainsKey("errorStyle")) {
                element.Remove("errorStyle");
            }

            newBody.Add(element);
        }

        // Update actions to reflect completed state (disable further interaction)
        if (root.ContainsKey("actions")) {
            var actions = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                JsonSerializer.Serialize(root["actions"])
            );
            
            if (actions != null) {
                // Try to detect which action was triggered, if any (e.g., QuickAnswerActivity "answer")
                string? selectedAnswer = null;
                if (userInputData != null && userInputData.TryGetValue("answer", out var selectedAnswerObj)) {
                    selectedAnswer = selectedAnswerObj?.ToString();
                }

                foreach (var action in actions) {
                    if (action.TryGetValue("type", out var actionType) && 
                        actionType?.ToString() == "Action.Submit") {
                        // Mark the action as disabled so the renderer can gray it out
                        action["isEnabled"] = false;

                        // If we know which answer was selected, visually highlight that button
                        if (!string.IsNullOrEmpty(selectedAnswer) && action.TryGetValue("data", out var dataObj)) {
                            try {
                                var dataJson = JsonSerializer.Serialize(dataObj);
                                var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson);
                                if (dataDict != null &&
                                    dataDict.TryGetValue("answer", out var answerObj) &&
                                    string.Equals(answerObj?.ToString(), selectedAnswer, StringComparison.Ordinal)) {
                                    action["style"] = "positive";
                                }
                            } catch {
                                // If anything goes wrong, just skip highlighting.
                            }
                        }
                    }
                }
                root["actions"] = actions;
            }
        }

        root["body"] = newBody;
        return JsonSerializer.Serialize(root);
    }

    /// <summary>
    /// Injects a `_metadata` object with `activityId` and `isRequired` into an Adaptive Card JSON string.
    /// </summary>
    /// <summary>
    /// Injects a `_metadata` object with `activityId` and `isRequired` into an Adaptive Card JSON string.
    /// </summary>
    public static string InjectMetadata(string cardJson, string activityId, bool isRequired, ILogger? logger = null) {
        try {
            using var document = JsonDocument.Parse(cardJson);
            var root = document.RootElement.Clone();

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream)) {
                writer.WriteStartObject();

                // ✅ Copy all existing top-level properties
                foreach (var property in root.EnumerateObject())
                    property.WriteTo(writer);

                // ✅ Add metadata section
                writer.WritePropertyName("_metadata");
                writer.WriteStartObject();
                writer.WriteString("activityId", activityId);
                writer.WriteBoolean("isRequired", isRequired);
                writer.WriteEndObject();

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        } catch (Exception ex) {
            logger?.LogError(ex, "[{ActivityId}] Failed to inject metadata into card JSON.", activityId);
            return cardJson; // fallback to original if parsing fails
        }
    }

}
