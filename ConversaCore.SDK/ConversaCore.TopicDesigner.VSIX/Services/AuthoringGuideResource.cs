using System.IO;
using System.Reflection;
using System.Text;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Provides access to the embedded ConversaCore Topic Authoring Guide.
    /// This is intended for use in prompts and tooling, not for runtime logic.
    /// </summary>
    internal static class AuthoringGuideResource
    {
        public const string Version = "0.1.0";

        private const string ResourceName = "ConversaCore.TopicTool.TopicAuthoringGuide.md";

        public static string GetGuideText()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    return string.Empty;
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
