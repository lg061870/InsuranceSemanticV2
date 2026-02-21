using System;
using System.IO;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Very lightweight structural validation for the ConversaCore topic registration helper.
    /// Ensures the guarded region contains at least one AddScoped&lt;ITopic&gt; registration.
    /// </summary>
    internal static class TopicRegistrationValidator
    {
        private const string StartMarker = "// <conversacore-domain-topics>";
        private const string EndMarker = "// </conversacore-domain-topics>";

        public static bool HasAnyTopicRegistrations(string registrationFilePath)
        {
            if (string.IsNullOrWhiteSpace(registrationFilePath))
                throw new ArgumentException("Registration file path must be provided.", nameof(registrationFilePath));

            if (!File.Exists(registrationFilePath))
                return false;

            var text = File.ReadAllText(registrationFilePath);
            var startIndex = text.IndexOf(StartMarker, StringComparison.Ordinal);
            var endIndex = text.IndexOf(EndMarker, StringComparison.Ordinal);
            if (startIndex < 0 || endIndex <= startIndex)
                return false;

            var region = text.Substring(startIndex, endIndex - startIndex);
            return region.IndexOf("AddScoped<ITopic>", StringComparison.Ordinal) >= 0;
        }
    }
}
