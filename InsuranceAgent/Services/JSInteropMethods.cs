using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace InsuranceAgent.Services; 
/// <summary>
/// Static methods exposed to JavaScript via Blazor JS interop.
/// Provides glue for triggering navigation or actions from the client side.
/// </summary>
public static class JSInteropMethods {
    private static NavigationEventService? _nav;

    /// <summary>
    /// Configures the JSInterop bridge with a navigation event service.
    /// Call this once during app startup (Program.cs).
    /// </summary>
    public static void Configure(NavigationEventService nav) {
        _nav = nav ?? throw new ArgumentNullException(nameof(nav));
        Console.WriteLine("[JSInterop] NavigationEventService configured.");
    }

    /// <summary>
    /// Launches the health questionnaire form (called from JavaScript).
    /// </summary>
    /// <param name="value">Optional data payload passed from JS.</param>
    [JSInvokable("LaunchHealthForm")]
    public static async Task LaunchHealthForm(object? value) {
        Console.WriteLine("[JSInterop] Health form event received from Bot:");
        Console.WriteLine(value?.ToString() ?? "<null>");

        if (_nav is null) {
            Console.Error.WriteLine("[JSInterop] NavigationEventService is not configured!");
            return;
        }

        await _nav.RequestNavigate("/health-questionnaire");
    }
}
