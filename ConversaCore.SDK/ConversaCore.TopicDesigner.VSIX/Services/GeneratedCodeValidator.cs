using System;
using System.Collections.Generic;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Lightweight safety checks for generated topic code.
    /// Intended to catch obviously unsafe or disallowed APIs before files are written.
    /// This is not a full security audit, just a guardrail for AI-generated content.
    /// </summary>
    internal static class GeneratedCodeValidator
    {
        // Minimal first-pass set; can be refined over time.
        private static readonly IReadOnlyList<string> DisallowedFragments = new[]
        {
            "System.IO.",
            "System.Net.Http.HttpClient",
            "System.Net.Sockets.",
            "Thread.Sleep(",
            "Task.Delay(" // DelayActivity should be used instead.
        };

        public static void ValidateOrThrow(string code)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));

            foreach (var fragment in DisallowedFragments)
            {
                if (code.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException(
                        $"Generated topic code uses disallowed API or namespace: '{fragment}'. " +
                        "Please adjust the prompt or manually refactor the topic.");
                }
            }
        }
    }
}
