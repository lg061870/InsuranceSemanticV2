using System;
using System.Text;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    internal static class VectorCollectionNameHelper
    {
        public static string GetCollectionName(string appPrefix, DocumentCollectionConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");

            var prefix = string.IsNullOrWhiteSpace(appPrefix) ? "app" : appPrefix;
            var safePrefix = Sanitize(prefix);
            var safeCollection = Sanitize(config.Name ?? "collection");

            if (config.Scope == DocumentCollectionScope.Topic)
            {
                var safeTopic = Sanitize(config.TopicName ?? "topic");
                return string.Format("{0}_topic_{1}_{2}", safePrefix, safeTopic, safeCollection);
            }

            return string.Format("{0}_global_{1}", safePrefix, safeCollection);
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return "";
            }

            var sb = new StringBuilder(input.Length);
            foreach (var ch in input.ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append('_');
                }
            }

            var result = sb.ToString();
            while (result.IndexOf("__", StringComparison.Ordinal) >= 0)
            {
                result = result.Replace("__", "_");
            }

            return result.Trim('_');
        }
    }
}
