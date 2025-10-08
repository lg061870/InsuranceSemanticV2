using System;
using System.Linq;
using System.Threading.Tasks;
using ConversaCore.Services;

namespace InsuranceAgent.Services {
    /// <summary>
    /// Service for handling navigation events between components.
    /// Acts as an event bus for navigation requests.
    /// </summary>
    public class NavigationEventService : INavigationEventService {
        /// <summary>
        /// Event raised when navigation is requested.
        /// </summary>
        public event Func<string, Task>? NavigateRequested;

        /// <summary>
        /// Request navigation to the specified URL.
        /// Invokes all registered handlers safely.
        /// </summary>
        public async Task RequestNavigate(string url) {
            if (string.IsNullOrWhiteSpace(url))
                return;

            var handlers = NavigateRequested;
            if (handlers is null)
                return;

            foreach (var handler in handlers.GetInvocationList().Cast<Func<string, Task>>()) {
                try {
                    await handler(url);
                } catch (Exception ex) {
                    Console.Error.WriteLine($"[NavigationEventService] Handler threw exception: {ex}");
                }
            }
        }
    }
}
