using System;
using System.IO;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Enforces the requirement that topics use the partial-class pattern
    /// (developer-owned .Domain.cs + tool-owned .Generated.cs).
    /// The tool will refuse to operate on existing topics that don't follow this pattern.
    /// </summary>
    internal static class TopicPartialPatternGuard
    {
        public static void EnsurePartialPattern(string topicFolder, TopicSpec spec)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            if (string.IsNullOrWhiteSpace(spec.Name))
                throw new ArgumentException("TopicSpec.Name must be set before validation.", nameof(spec));

            if (string.IsNullOrWhiteSpace(topicFolder) || !Directory.Exists(topicFolder))
            {
                // No folder yet; nothing to validate.
                return;
            }

            var domainPath = Path.Combine(topicFolder, $"{spec.Name}.Domain.cs");
            var generatedPath = Path.Combine(topicFolder, $"{spec.Name}.Generated.cs");

            if (!File.Exists(domainPath) && !File.Exists(generatedPath))
            {
                // No existing files; this is effectively a new topic scenario.
                return;
            }

            var className = spec.Name.Trim() + "Topic";
            var expectedFragment = $"partial class {className}";

            if (File.Exists(domainPath))
            {
                var text = File.ReadAllText(domainPath);
                if (text.IndexOf(expectedFragment, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException($"Existing topic domain file does not use required partial class pattern (expected '{expectedFragment}' in {Path.GetFileName(domainPath)}).");
                }
            }

            if (File.Exists(generatedPath))
            {
                var text = File.ReadAllText(generatedPath);
                if (text.IndexOf(expectedFragment, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException($"Existing topic generated file does not use required partial class pattern (expected '{expectedFragment}' in {Path.GetFileName(generatedPath)}).");
                }
            }
        }
    }
}
