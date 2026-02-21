using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Helper for reading and writing the .conversacore.documents.json configuration file
    /// that describes document collections for vector embedding.
    /// </summary>
    internal static class DocumentConfigStorage
    {
        private const string DefaultConfigFileName = ".conversacore.documents.json";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        static DocumentConfigStorage()
        {
            Settings.Converters.Add(new StringEnumConverter());
        }

        /// <summary>
        /// Returns the default config path under a given host project root.
        /// </summary>
        public static string GetDefaultConfigPath(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root must be provided.", nameof(projectRoot));

            return Path.Combine(projectRoot, DefaultConfigFileName);
        }

        /// <summary>
        /// Load configuration from the given path, returning an empty root if the
        /// file does not exist.
        /// </summary>
        public static DocumentCollectionsConfigRoot Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Configuration path must be provided.", nameof(path));

            if (!File.Exists(path))
            {
                return new DocumentCollectionsConfigRoot();
            }

            var json = File.ReadAllText(path);
            var root = JsonConvert.DeserializeObject<DocumentCollectionsConfigRoot>(json, Settings);
            return root ?? new DocumentCollectionsConfigRoot();
        }

        /// <summary>
        /// Persist configuration to the given path, creating directories as needed.
        /// </summary>
        public static void Save(string path, DocumentCollectionsConfigRoot root)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Configuration path must be provided.", nameof(path));

            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(root, Settings);
            File.WriteAllText(path, json);
        }
    }
}
