using System;
using Microsoft.SemanticKernel;

namespace InsuranceAgent.Services;

/// <summary>
/// Factory for creating and configuring Semantic Kernel instances.
/// Centralizes Semantic Kernel initialization so you can swap or extend later.
/// </summary>
public static class KernelFactory {
    /// <summary>
    /// Creates a new Semantic Kernel instance.
    /// </summary>
    /// <returns>A configured Semantic Kernel instance.</returns>
    public static Kernel CreateKernel() {
        try {
            // Create a new Kernel (v1.64.0 style)
            var kernel = new Kernel();

            // TODO: Configure kernel here if needed
            // Examples:
            //   kernel.Plugins.Add(new YourCustomPlugin());
            //   kernel.Config.AddAzureOpenAIChatCompletion("modelId", "deploymentName", "endpoint", "apiKey");

            return kernel;
        } catch (Exception ex) {
            Console.Error.WriteLine($"[KernelFactory] Error creating Semantic Kernel: {ex}");
            throw new InvalidOperationException(
                "Failed to create Semantic Kernel instance. Check version compatibility and configuration.",
                ex
            );
        }
    }
}
