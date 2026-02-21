using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Generates per-collection ingestion services and a coordinator in the host project
    /// based on the .conversacore.documents.json configuration.
    /// Also updates the central registration helper with DI registrations.
    /// </summary>
    internal static class IngestionCodeGenerator
    {
        private const string IngestionStartMarker = "// <conversacore-document-ingestion>";
        private const string IngestionEndMarker = "// </conversacore-document-ingestion>";

        public static void Generate(string projectRoot, string registrationFilePath, DocumentCollectionsConfigRoot config)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root must be provided.", nameof(projectRoot));

            if (string.IsNullOrWhiteSpace(registrationFilePath))
                throw new ArgumentException("Registration file path must be provided.", nameof(registrationFilePath));

            if (config == null || config.Collections == null || config.Collections.Count == 0)
                return;

            if (!File.Exists(registrationFilePath))
                throw new FileNotFoundException("Registration file not found.", registrationFilePath);

            var registrationText = File.ReadAllText(registrationFilePath);
            var registrationNamespace = ParseNamespace(registrationText);
            if (string.IsNullOrWhiteSpace(registrationNamespace))
                throw new InvalidOperationException("Could not determine namespace from registration file.");

            var baseNamespace = registrationNamespace;
            const string configurationSuffix = ".Configuration";
            if (baseNamespace.EndsWith(configurationSuffix, StringComparison.Ordinal))
            {
                baseNamespace = baseNamespace.Substring(0, baseNamespace.Length - configurationSuffix.Length);
            }

            var servicesNamespace = baseNamespace + ".Services";

            var servicesFolder = Path.Combine(projectRoot, "Services");
            var generatedFolder = Path.Combine(servicesFolder, "Generated");
            Directory.CreateDirectory(generatedFolder);

            var appPrefix = config.AppPrefix;
            var serviceTypeNames = new List<string>();

            foreach (var collection in config.Collections)
            {
                var className = BuildServiceClassName(collection);
                var filePath = Path.Combine(generatedFolder, className + ".generated.cs");
                var vectorCollectionName = VectorCollectionNameHelper.GetCollectionName(appPrefix, collection);

                var code = GenerateEmbeddingServiceCode(servicesNamespace, className, collection, vectorCollectionName);
                File.WriteAllText(filePath, code, Encoding.UTF8);

                serviceTypeNames.Add(servicesNamespace + "." + className);
            }

            // Coordinator
            const string coordinatorClassName = "DocumentIngestionCoordinator";
            var coordinatorPath = Path.Combine(generatedFolder, coordinatorClassName + ".generated.cs");
            var coordinatorCode = GenerateCoordinatorCode(servicesNamespace, coordinatorClassName, config, serviceTypeNames);
            File.WriteAllText(coordinatorPath, coordinatorCode, Encoding.UTF8);

            UpdateRegistrationFile(registrationFilePath, registrationText, servicesNamespace, coordinatorClassName, serviceTypeNames);
        }

        private static string ParseNamespace(string fileText)
        {
            if (string.IsNullOrEmpty(fileText))
                return string.Empty;

            const string nsToken = "namespace ";
            var index = fileText.IndexOf(nsToken, StringComparison.Ordinal);
            if (index < 0)
                return string.Empty;

            var start = index + nsToken.Length;
            var end = fileText.IndexOfAny(new[] { '\r', '\n', ' ' }, start);
            if (end < 0)
                end = fileText.Length;

            var ns = fileText.Substring(start, end - start).Trim();
            return ns;
        }

        private static string BuildServiceClassName(DocumentCollectionConfig collection)
        {
            var name = string.IsNullOrWhiteSpace(collection.Name) ? "Collection" : collection.Name.Trim();
            var parts = name
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                if (part.Length == 1)
                    sb.Append(char.ToUpperInvariant(part[0]));
                else
                    sb.Append(char.ToUpperInvariant(part[0])).Append(part.Substring(1));
            }

            if (sb.Length == 0)
                sb.Append("Collection");

            sb.Append("DocumentsEmbeddingService");
            return sb.ToString();
        }

        private static string GenerateEmbeddingServiceCode(string servicesNamespace, string className, DocumentCollectionConfig collection, string vectorCollectionName)
        {
            var folders = collection.SourceFolders ?? new List<string>();
            var includes = collection.IncludePatterns ?? new List<string>();

            if (includes.Count == 0)
            {
                includes.Add("*.*");
            }

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using ConversaCore.Interfaces;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine();
            sb.AppendLine("namespace " + servicesNamespace);
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Auto-generated ingestion service for document collection '" + (collection.Name ?? string.Empty) + "'.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public class " + className);
            sb.AppendLine("    {");
            sb.AppendLine("        public const string CollectionName = \"" + vectorCollectionName + "\";");
            sb.AppendLine();
            sb.AppendLine("        private readonly IDocumentEmbeddingService _embeddingService;");
            sb.AppendLine("        private readonly IDocumentProcessingService _docProcessor;");
            sb.AppendLine("        private readonly IVectorDatabaseService _vectorDb;");
            sb.AppendLine("        private readonly ILogger<" + className + "> _logger;");
            sb.AppendLine();
            sb.AppendLine("        private readonly string[] _sourceFolders = new[]");
            sb.AppendLine("        {");
            for (var i = 0; i < folders.Count; i++)
            {
                var folder = folders[i] ?? string.Empty;
                sb.Append("            \"").Append(folder.Replace("\\", "/")).Append("\"");
                if (i < folders.Count - 1) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        private readonly string[] _includePatterns = new[]");
            sb.AppendLine("        {");
            for (var i = 0; i < includes.Count; i++)
            {
                var pattern = includes[i] ?? string.Empty;
                sb.Append("            \"").Append(pattern.Replace("\\", "/")).Append("\"");
                if (i < includes.Count - 1) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        public " + className + "(");
            sb.AppendLine("            IDocumentEmbeddingService embeddingService,");
            sb.AppendLine("            IDocumentProcessingService docProcessor,");
            sb.AppendLine("            IVectorDatabaseService vectorDb,");
            sb.AppendLine("            ILogger<" + className + "> logger)");
            sb.AppendLine("        {");
            sb.AppendLine("            _embeddingService = embeddingService ?? throw new ArgumentNullException(\"embeddingService\");");
            sb.AppendLine("            _docProcessor = docProcessor ?? throw new ArgumentNullException(\"docProcessor\");");
            sb.AppendLine("            _vectorDb = vectorDb ?? throw new ArgumentNullException(\"vectorDb\");");
            sb.AppendLine("            _logger = logger ?? throw new ArgumentNullException(\"logger\");");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public async Task<int> SyncAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            var totalStored = 0;");
            sb.AppendLine("            var root = _embeddingService.DocumentsPath;");
            sb.AppendLine("            foreach (var relative in _sourceFolders)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (string.IsNullOrWhiteSpace(relative)) continue;");
            sb.AppendLine("                var dir = Path.Combine(root, relative);");
            sb.AppendLine("                if (!Directory.Exists(dir))");
            sb.AppendLine("                {");
            sb.AppendLine("                    _logger.LogWarning(\"Directory not found for collection {Collection}: {Dir}\", CollectionName, dir);");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                foreach (var pattern in _includePatterns)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var chunks = await _docProcessor.ProcessDirectoryAsync(dir, pattern, cancellationToken: cancellationToken);");
            sb.AppendLine("                    if (chunks.Count == 0) continue;");
            sb.AppendLine();
            sb.AppendLine("                    var stored = await _vectorDb.StoreBatchAsync(CollectionName, chunks, cancellationToken);");
            sb.AppendLine("                    totalStored += stored;");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            _logger.LogInformation(\"Indexed {Stored} chunks for collection {Collection}\", totalStored, CollectionName);");
            sb.AppendLine("            return totalStored;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateCoordinatorCode(string servicesNamespace, string className, DocumentCollectionsConfigRoot config, List<string> serviceTypeNames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
            sb.AppendLine("namespace " + servicesNamespace);
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Auto-generated coordinator for running document ingestion across collections.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public class " + className);
            sb.AppendLine("    {");

            // Fields
            for (var i = 0; i < serviceTypeNames.Count; i++)
            {
                var fullName = serviceTypeNames[i];
                var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
                sb.AppendLine("        private readonly " + simpleName + " _" + ToCamelCase(simpleName) + ";");
            }
            sb.AppendLine();

            // Constructor
            sb.Append("        public " + className + "(");
            for (var i = 0; i < serviceTypeNames.Count; i++)
            {
                var fullName = serviceTypeNames[i];
                var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
                sb.Append(simpleName + " " + ToCamelCase(simpleName));
                if (i < serviceTypeNames.Count - 1)
                    sb.Append(", ");
            }
            sb.AppendLine(")");
            sb.AppendLine("        {");
            for (var i = 0; i < serviceTypeNames.Count; i++)
            {
                var fullName = serviceTypeNames[i];
                var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
                var fieldName = ToCamelCase(simpleName);
                sb.AppendLine("            _" + fieldName + " = " + fieldName + " ?? throw new ArgumentNullException(\"" + fieldName + "\");");
            }
            sb.AppendLine("        }");
            sb.AppendLine();

            // Run single collection by logical name
            sb.AppendLine("        public async Task<int> RunCollectionAsync(string name, CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (string.IsNullOrWhiteSpace(name)) return 0;");
            sb.AppendLine("            name = name.Trim();");
            sb.AppendLine();
            sb.AppendLine("            switch (name)");
            sb.AppendLine("            {");

            for (var i = 0; i < config.Collections.Count; i++)
            {
                var col = config.Collections[i];
                if (i >= serviceTypeNames.Count) break;
                var fullName = serviceTypeNames[i];
                var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
                var fieldName = ToCamelCase(simpleName);
                var logicalName = (col.Name ?? string.Empty).Trim();
                if (logicalName.Length == 0) logicalName = simpleName;

                sb.AppendLine("                case \"" + logicalName + "\":");
                sb.AppendLine("                    return await _" + fieldName + ".SyncAsync(cancellationToken);" );
            }

            sb.AppendLine("                default:");
            sb.AppendLine("                    return 0;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Run all collections for a topic
            sb.AppendLine("        public async Task<int> RunTopicCollectionsAsync(string topicName, CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (string.IsNullOrWhiteSpace(topicName)) return 0;");
            sb.AppendLine("            topicName = topicName.Trim();");
            sb.AppendLine("            var total = 0;");

            for (var i = 0; i < config.Collections.Count && i < serviceTypeNames.Count; i++)
            {
                var col = config.Collections[i];
                if (col.Scope != DocumentCollectionScope.Topic) continue;
                var fullName = serviceTypeNames[i];
                var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
                var fieldName = ToCamelCase(simpleName);
                var colTopic = (col.TopicName ?? string.Empty).Trim();
                if (colTopic.Length == 0) continue;

                sb.AppendLine("            if (string.Equals(topicName, \"" + colTopic + "\", StringComparison.OrdinalIgnoreCase))");
                sb.AppendLine("            {");
                sb.AppendLine("                total += await _" + fieldName + ".SyncAsync(cancellationToken);");
                sb.AppendLine("            }");
            }

            sb.AppendLine("            return total;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Run all global collections
            sb.AppendLine("        public async Task<int> RunGlobalCollectionsAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            var total = 0;");

            for (var i = 0; i < config.Collections.Count && i < serviceTypeNames.Count; i++)
            {
                var col = config.Collections[i];
                if (col.Scope != DocumentCollectionScope.Global) continue;
                var fullName = serviceTypeNames[i];
                var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
                var fieldName = ToCamelCase(simpleName);

                sb.AppendLine("            total += await _" + fieldName + ".SyncAsync(cancellationToken);");
            }

            sb.AppendLine("            return total;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Run all collections
            sb.AppendLine("        public async Task<int> RunAllCollectionsAsync(CancellationToken cancellationToken = default)");
            sb.AppendLine("        {");
            sb.AppendLine("            var total = 0;");

            for (var i = 0; i < serviceTypeNames.Count; i++)
            {
                var fullName = serviceTypeNames[i];
                var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
                var fieldName = ToCamelCase(simpleName);

                sb.AppendLine("            total += await _" + fieldName + ".SyncAsync(cancellationToken);");
            }

            sb.AppendLine("            return total;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.Length == 1) return name.ToLowerInvariant();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static void UpdateRegistrationFile(string registrationFilePath, string originalText, string servicesNamespace, string coordinatorClassName, List<string> serviceTypeNames)
        {
            var text = originalText;

            // Ensure ingestion method and markers exist
            if (text.IndexOf(IngestionStartMarker, StringComparison.Ordinal) < 0 ||
                text.IndexOf(IngestionEndMarker, StringComparison.Ordinal) < 0)
            {
                const string methodSignature = "public static void AddConversaCoreDocumentIngestion";
                var classEnd = text.LastIndexOf('}', text.Length - 1);
                if (classEnd <= 0)
                    throw new InvalidOperationException("Could not find end of registration class to insert ingestion method.");

                var methodIndentation = "        ";
                var methodBuilder = new StringBuilder();
                methodBuilder.AppendLine();
                methodBuilder.AppendLine(methodIndentation + "public static void AddConversaCoreDocumentIngestion(this IServiceCollection services)");
                methodBuilder.AppendLine(methodIndentation + "{");
                methodBuilder.AppendLine(methodIndentation + "    " + IngestionStartMarker);
                methodBuilder.AppendLine(methodIndentation + "    " + IngestionEndMarker);
                methodBuilder.AppendLine(methodIndentation + "}");

                text = text.Insert(classEnd, methodBuilder.ToString());
            }

            var startIndex = text.IndexOf(IngestionStartMarker, StringComparison.Ordinal);
            var endIndex = text.IndexOf(IngestionEndMarker, StringComparison.Ordinal);
            if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
            {
                throw new InvalidOperationException("Registration file does not contain expected document ingestion markers.");
            }

            // Insert registrations just before end marker
            var insertPos = endIndex;

            var indentationLineStart = text.LastIndexOf('\n', startIndex);
            if (indentationLineStart < 0) indentationLineStart = 0;
            var nextLineStart = text.IndexOf('\n', startIndex + IngestionStartMarker.Length);
            if (nextLineStart < 0) nextLineStart = startIndex + IngestionStartMarker.Length;
            var line = text.Substring(indentationLineStart + 1, nextLineStart - indentationLineStart - 1);
            var leading = new string(line.TakeWhile(char.IsWhiteSpace).ToArray());
            var indentation = string.IsNullOrEmpty(leading) ? "        " : leading;

            var snippet = new StringBuilder();
            snippet.AppendLine();
            foreach (var fullType in serviceTypeNames)
            {
                snippet.AppendLine(indentation + "services.AddScoped<" + fullType + ">();");
            }
            snippet.AppendLine(indentation + "services.AddScoped<" + servicesNamespace + "." + coordinatorClassName + ">();");

            text = text.Insert(insertPos, snippet.ToString());
            File.WriteAllText(registrationFilePath, text, Encoding.UTF8);
        }
    }
}
