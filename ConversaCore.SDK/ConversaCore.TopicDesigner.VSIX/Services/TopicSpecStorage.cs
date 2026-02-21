using System;
using System.IO;
using ConversaCore.TopicTool.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Helper for reading and writing TopicSpec JSON files on disk.
    /// </summary>
    internal static class TopicSpecStorage
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public static TopicSpec Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must be provided", nameof(path));

            var json = File.ReadAllText(path);
            var spec = JsonConvert.DeserializeObject<TopicSpec>(json, Settings);
            return spec ?? new TopicSpec();
        }

        public static void Save(string path, TopicSpec spec)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must be provided", nameof(path));

            if (spec is null)
                throw new ArgumentNullException(nameof(spec));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(spec, Settings);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Convenience helper to get the default spec file path for a topic folder.
        /// </summary>
        public static string GetDefaultSpecPath(string topicFolder)
        {
            if (string.IsNullOrWhiteSpace(topicFolder))
                throw new ArgumentException("Topic folder must be provided", nameof(topicFolder));

            return Path.Combine(topicFolder, "topic.spec.json");
        }
    }
}
