using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ConversaCore.TopicFlow.Rules;
using InsuranceAgent.Services;

namespace RuleIndexerRunner
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<IRuleIndexer, RuleIndexerSample>();
                services.AddSingleton<IRuleStore>(sp => {
                    // attempt to locate an existing canonical file in ancestor folders
                    var start = Directory.GetCurrentDirectory();
                    var dir = new DirectoryInfo(start);
                    while (dir != null)
                    {
                        var candidate = Path.Combine(dir.FullName, "InsuranceAgent", "jsonrules", "TERM_INS_RULES_canonical.jsonl");
                        if (File.Exists(candidate)) return new RuleStoreSample(candidate);
                        dir = dir.Parent;
                    }

                    // fallback: empty store
                    return new RuleStoreSample(string.Empty);
                });

                var sp = services.BuildServiceProvider();
                var indexer = sp.GetRequiredService<IRuleIndexer>();

                var root = Directory.GetCurrentDirectory();
                var source = FindUpwards(root, Path.Combine("InsuranceAgent", "jsonrules", "TERM_INS_RULES.json"));
                Console.WriteLine($"Source: {source}");

                var count = await indexer.IndexRulesAsync(source, CancellationToken.None);
                Console.WriteLine($"Indexed {count} canonical chunks.");

                var canonicalPath = Path.Combine(Path.GetDirectoryName(source) ?? ".", Path.GetFileNameWithoutExtension(source) + "_canonical.jsonl");
                var store = new RuleStoreSample(canonicalPath);

                var evidence = File.ReadAllText(source);
                var sampleEvidence = evidence.Length > 800 ? evidence.Substring(0, 800) : evidence;

                var candidates = await store.RetrieveCandidatesAsync(sampleEvidence, null, 12, CancellationToken.None);
                Console.WriteLine($"Retrieved {candidates.Count} candidates. Top results:");
                foreach (var c in candidates)
                {
                    Console.WriteLine($"- {c.RuleId} | score={c.Score} | carrier={c.Carrier} | summary={c.Summary}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 2;
            }
        }

        static string FindUpwards(string startDir, string relativePath)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(relativePath);
        }
    }
}
