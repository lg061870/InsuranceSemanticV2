using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using ConversaCore.Tools;

namespace ConversaCore.Registration;

/// <summary>Tool descriptor and catalog registration extensions.</summary>
public static class ConversaCoreBuilderToolExtensions
{
    /// <summary>Registers one immutable tool descriptor and its catalog.</summary>
    public static ConversaCoreBuilder AddTool(this ConversaCoreBuilder builder, ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(descriptor);
        builder.Services.AddSingleton(descriptor);
        builder.Services.Replace(ServiceDescriptor.Singleton<IToolCatalog>(sp =>
            new ToolCatalog(sp.GetServices<ToolDescriptor>())));
        return builder;
    }

    /// <summary>Scans the containing assembly for attributed tool implementations without constructing them.</summary>
    public static ConversaCoreBuilder AddToolsFromAssemblyContaining<TMarker>(this ConversaCoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var assembly = typeof(TMarker).Assembly;
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            var attribute = type.GetCustomAttribute<ConversaToolAttribute>();
            var contract = type.GetInterfaces().SingleOrDefault(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConversaTool<,>));
            if (attribute is null) continue;
            if (contract is null)
                throw new InvalidOperationException($"Tool type '{type.FullName}' has ConversaToolAttribute but does not implement IConversaTool<TRequest,TResult>.");

            builder.AddTool(new ToolDescriptor(attribute.ToolId, attribute.Version, attribute.DisplayName,
                attribute.Description, contract.GetGenericArguments()[0], contract.GetGenericArguments()[1], implementationType: type));
        }
        return builder;
    }
}
