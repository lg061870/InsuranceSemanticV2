using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

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
                errors[jsonFieldName].Add(result.ErrorMessage ?? "Validation error");
            }
        }

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
            bool hasErrors = false;
            List<string>? fieldErrors = null;
            string? errorId = null;
            
            if (element.TryGetValue("id", out var errorIdObj)) {
                errorId = errorIdObj?.ToString()?.ToLowerInvariant();
                // Console.WriteLine($"[DEBUG] Checking card element ID: '{errorId}'");
                if (!string.IsNullOrEmpty(errorId) && errors.ContainsKey(errorId)) {
                    hasErrors = true;
                    fieldErrors = errors[errorId].Where(e => e != null).ToList()!;
                    
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

            // Attach error messages immediately after the input element
            if (hasErrors && fieldErrors != null && !string.IsNullOrEmpty(errorId)) {
                // add error messages below
                foreach (var msg in fieldErrors) {
                    newBody.Add(new Dictionary<string, object> {
                        ["type"] = "TextBlock",
                        ["text"] = $"⚠ {msg}",
                        ["wrap"] = true,
                        ["color"] = "Attention",
                        ["size"] = "Small",
                        ["spacing"] = "None",
                        ["id"] = $"{errorId}_error",
                        ["isSubtle"] = true
                    });
                }

                // remove from errors so we can later detect leftovers
                errors.Remove(errorId);
            }
        }

        // If some errors could not be matched to a card element → throw hard error
        if (errors.Any()) {
            var unmatched = string.Join(", ",
                errors.Select(kv => $"{kv.Key}: {string.Join(" | ", kv.Value)}"));
            throw new InvalidOperationException(
                $"Validation produced errors for fields not found in AdaptiveCard JSON: {unmatched}. " +
                $"Make sure property names match card element ids (case-insensitive).");
        }

        root["body"] = newBody;
        return JsonSerializer.Serialize(root);
    }

    /// <summary>
    /// Creates a "success" version of the card with user data preserved,
    /// all inputs disabled, and submit button changed to "Done".
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
            // Skip any existing error messages (TextBlocks with Attention color)
            if (element.TryGetValue("type", out var elementTypeObj) &&
                elementTypeObj?.ToString() == "TextBlock" &&
                element.TryGetValue("color", out var colorObj) && 
                colorObj?.ToString() == "Attention") {
                continue; // Skip error TextBlocks only
            }

            // Preserve user input and disable all input elements
            if (element.TryGetValue("id", out var idObj)) {
                var id = idObj?.ToString();
                if (!string.IsNullOrEmpty(id) && userInputData.ContainsKey(id)) {
                    var inputType = element.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : "";
                    var userValue = userInputData[id];
                    
                    switch (inputType) {
                        case "Input.Text":
                        case "Input.Number":
                        case "Input.Date":
                            element["value"] = userValue?.ToString() ?? "";
                            element["isEnabled"] = false; // Disable input
                            break;
                        case "Input.ChoiceSet":
                            if (userValue != null) {
                                element["value"] = userValue.ToString() ?? "";
                            }
                            element["isEnabled"] = false; // Disable choice set
                            break;
                        case "Input.Toggle":
                            if (userValue is bool boolValue) {
                                element["value"] = boolValue;
                            } else if (bool.TryParse(userValue?.ToString(), out var parsedBool)) {
                                element["value"] = parsedBool;
                            }
                            element["isEnabled"] = false; // Disable toggle
                            break;
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

        // Update actions to show "Done" instead of "Submit"
        if (root.ContainsKey("actions")) {
            var actions = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                JsonSerializer.Serialize(root["actions"])
            );
            
            if (actions != null) {
                foreach (var action in actions) {
                    if (action.TryGetValue("type", out var actionType) && 
                        actionType?.ToString() == "Action.Submit") {
                        action["title"] = "Done";
                        action["style"] = "positive";
                        // Optionally disable the action or change its data
                        if (action.ContainsKey("data")) {
                            if (action["data"] is Dictionary<string, object> actionData) {
                                actionData["action"] = "completed";
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
}
