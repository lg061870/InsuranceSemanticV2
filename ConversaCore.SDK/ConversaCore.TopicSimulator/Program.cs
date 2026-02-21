using System.Reflection;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Core;
using Microsoft.Extensions.Logging;

// Simple runtime harness to execute a single TopicFlow in isolation.
// Usage:
//   dotnet run --project ConversaCore.SDK/ConversaCore.TopicSimulator -- \
//      <assemblyPath> <topicFullName>
//
// If no arguments are provided, it defaults to the SDK SampleTopic.

var assemblyPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ConversaCore.BlazorTemplateHost", "bin", "Debug", "net9.0", "ConversaCore.BlazorTemplateHost.dll");

var topicTypeName = args.Length > 1
    ? args[1]
    : "ConversaCore.BlazorTemplateHost.Topics.SampleTopic.SampleTopic";

if (!File.Exists(assemblyPath))
{
    Console.WriteLine($"[TopicSimulator] Assembly not found: {assemblyPath}");
    return;
}

Console.WriteLine($"[TopicSimulator] Loading assembly: {assemblyPath}");
var assembly = Assembly.LoadFrom(assemblyPath);
var topicType = assembly.GetType(topicTypeName);

if (topicType == null)
{
    Console.WriteLine($"[TopicSimulator] Topic type not found: {topicTypeName}");
    return;
}

if (!typeof(TopicFlow).IsAssignableFrom(topicType))
{
    Console.WriteLine($"[TopicSimulator] Type is not a TopicFlow: {topicTypeName}");
    return;
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        })
        .SetMinimumLevel(LogLevel.Information);
});

var wfContext = new TopicWorkflowContext();
var conversationLogger = loggerFactory.CreateLogger<ConversationContext>();
var conversationContext = new ConversationContext("sim-conversation", "sim-user", conversationLogger);

// Create ILogger<TopicType> via reflection
var createLoggerMethod = typeof(LoggerFactoryExtensions)
    .GetMethods(BindingFlags.Public | BindingFlags.Static)
    .Single(m => m.Name == "CreateLogger" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);

var genericCreateLogger = createLoggerMethod.MakeGenericMethod(topicType);
var topicLogger = genericCreateLogger.Invoke(null, new object[] { loggerFactory });

Console.WriteLine($"[TopicSimulator] Creating topic instance: {topicType.FullName}");

var topicInstance = Activator.CreateInstance(topicType, wfContext, topicLogger!, conversationContext) as TopicFlow;
if (topicInstance == null)
{
    Console.WriteLine("[TopicSimulator] Failed to create topic instance (constructor mismatch?).");
    return;
}

Console.WriteLine("[TopicSimulator] Starting topic RunAsync() ...");

try
{
    var result = await topicInstance.RunAsync();
    Console.WriteLine($"[TopicSimulator] Topic completed. IsCompleted: {result.IsCompleted}, IsHandled: {result.IsHandled}, Response: {result.Response}");
}
catch (Exception ex)
{
    Console.WriteLine($"[TopicSimulator] Exception during topic execution: {ex}");
}

Console.WriteLine("[TopicSimulator] Workflow context snapshot:");
Console.WriteLine(wfContext.ToString());
