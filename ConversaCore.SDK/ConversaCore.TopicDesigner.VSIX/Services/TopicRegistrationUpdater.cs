using System;
using System.IO;
using System.Linq;
using System.Text;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Updates a centralized ConversaCore topic registration helper file.
    /// It looks for markers // &lt;conversacore-domain-topics&gt; and // &lt;/conversacore-domain-topics&gt;
    /// and appends AddScoped registrations for the given topic if not already present.
    /// </summary>
    internal static class TopicRegistrationUpdater
    {
        private const string StartMarker = "// <conversacore-domain-topics>";
        private const string EndMarker = "// </conversacore-domain-topics>";

        public static void AddOrUpdateTopicRegistration(string registrationFilePath, TopicSpec spec)
        {
            if (string.IsNullOrWhiteSpace(registrationFilePath))
                throw new ArgumentException("Registration file path must be provided.", nameof(registrationFilePath));

            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            if (string.IsNullOrWhiteSpace(spec.Name))
                throw new ArgumentException("TopicSpec.Name must be set before registration.", nameof(spec));

            if (!File.Exists(registrationFilePath))
                throw new FileNotFoundException("Registration file not found.", registrationFilePath);

            var text = File.ReadAllText(registrationFilePath);
            var startIndex = text.IndexOf(StartMarker, StringComparison.Ordinal);
            var endIndex = text.IndexOf(EndMarker, StringComparison.Ordinal);

            if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
            {
                throw new InvalidOperationException("Registration file does not contain expected ConversaCore markers.");
            }

            var insertPos = endIndex; // insert just before end marker
            var className = spec.Name.Trim() + "Topic";

            // Avoid duplicate registrations
            if (text.Contains(className))
            {
                return;
            }

            var ns = string.IsNullOrWhiteSpace(spec.Namespace)
                ? "YourApp.Topics"
                : spec.Namespace.Trim();

            var indentation = DetectIndentation(text, startIndex);

            var snippet = new StringBuilder();
            snippet.AppendLine();
            snippet.AppendLine(indentation + $"services.AddScoped<{ns}.{className}>();");
            snippet.AppendLine(indentation + $"services.AddScoped<ITopic>(sp => sp.GetRequiredService<{ns}.{className}>());");

            text = text.Insert(insertPos, snippet.ToString());
            File.WriteAllText(registrationFilePath, text, Encoding.UTF8);

            // Basic structural validation: ensure at least one ITopic registration exists.
            if (!TopicRegistrationValidator.HasAnyTopicRegistrations(registrationFilePath))
            {
                throw new InvalidOperationException("Registration file does not contain any AddScoped<ITopic> registrations in the guarded region.");
            }
        }

        private static string DetectIndentation(string text, int startIndex)
        {
            var lineStart = text.LastIndexOf('\n', startIndex);
            if (lineStart < 0) lineStart = 0;
            var nextLineStart = text.IndexOf('\n', startIndex);
            if (nextLineStart < 0 || nextLineStart <= lineStart)
                return "        "; // default 8 spaces

            var line = text.Substring(lineStart + 1, nextLineStart - lineStart - 1);
            var leading = new string(line.TakeWhile(char.IsWhiteSpace).ToArray());
            return string.IsNullOrEmpty(leading) ? "        " : leading;
        }
    }
}
