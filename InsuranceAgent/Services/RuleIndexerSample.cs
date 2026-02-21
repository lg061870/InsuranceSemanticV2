using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.TopicFlow.Rules;

namespace InsuranceAgent.Services
{
    /// <summary>
    /// Simple file-based rule indexer that normalizes JSON rule files into canonical chunks.
    /// Writes canonical chunks to a JSONL file next to the source by appending "_canonical.jsonl".
    /// This is a lightweight developer tool; production indexers should create embeddings and write to a vector store.
    /// </summary>
    public class RuleIndexerSample : IRuleIndexer
    {
        public async Task<int> IndexRulesAsync(string sourceFilePath, CancellationToken ct = default)
        {
            if (!File.Exists(sourceFilePath)) throw new FileNotFoundException(sourceFilePath);

            using var stream = File.OpenRead(sourceFilePath);
            var doc = await JsonNode.ParseAsync(stream, cancellationToken: ct);

            if (doc == null || doc is not JsonArray arr)
            {
                throw new InvalidOperationException("Expected top-level JSON array of rule entries.");
            }

            var outPath = Path.Combine(Path.GetDirectoryName(sourceFilePath) ?? ".", Path.GetFileNameWithoutExtension(sourceFilePath) + "_canonical.jsonl");

            var count = 0;
            await using var outStream = new StreamWriter(outPath, false);

            foreach (var item in arr)
            {
                if (ct.IsCancellationRequested) break;

                if (item is JsonObject obj)
                {
                    var carrier = obj["CARRIER"]?.ToString() ?? string.Empty;
                    foreach (var prop in obj)
                    {
                        var key = prop.Key;
                        if (key == "CARRIER" || key == "AGE_RANGE" || key == "PRODUCT_TYPE" || key == "BUILD_CHART__UW_GUIDE" || key == "PAYMENT_METHODS") continue;

                        var value = prop.Value;
                        if (value == null) continue;

                        if (value is JsonObject nested)
                        {
                            foreach (var inner in nested)
                            {
                                var cr = new CanonicalRule
                                {
                                    RuleId = Guid.NewGuid().ToString("D"),
                                    ChunkId = Guid.NewGuid().ToString("D"),
                                    SourceFile = Path.GetFileName(sourceFilePath),
                                    Carrier = carrier,
                                    SectionPath = key,
                                    PredicateKey = inner.Key,
                                    PredicateValue = inner.Value?.ToString() ?? string.Empty,
                                    RawText = inner.Value?.ToString() ?? string.Empty,
                                    Summary = Truncate(inner.Value?.ToString() ?? string.Empty, 240),
                                    Weight = 1.0
                                };

                                var line = JsonSerializer.Serialize(cr);
                                await outStream.WriteLineAsync(line);
                                count++;
                            }
                        }
                        else
                        {
                            var cr = new CanonicalRule
                            {
                                RuleId = Guid.NewGuid().ToString("D"),
                                ChunkId = Guid.NewGuid().ToString("D"),
                                SourceFile = Path.GetFileName(sourceFilePath),
                                Carrier = carrier,
                                SectionPath = "root",
                                PredicateKey = key,
                                PredicateValue = value.ToString() ?? string.Empty,
                                RawText = value.ToString() ?? string.Empty,
                                Summary = Truncate(value.ToString() ?? string.Empty, 240),
                                Weight = 1.0
                            };

                            var line = JsonSerializer.Serialize(cr);
                            await outStream.WriteLineAsync(line);
                            count++;
                        }
                    }
                }
            }

            await outStream.FlushAsync();
            return count;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
