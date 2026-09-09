using ConversaCore.Tools;

namespace ConversaCore.Tests.Tools;

public sealed class ToolPolicyMetadataTests
{
    [Fact]
    public void Descriptor_RetainsImmutablePolicyMetadata()
    {
        var descriptor = new ToolDescriptor("lead.create", "1.0", "Create lead", "Creates a lead",
            typeof(string), typeof(Guid), sideEffect: ToolSideEffect.Mutating,
            authorization: new ToolAuthorizationPolicy { PolicyName = "lead.write", RequiredClaims = new HashSet<string> { "lead:create" } },
            confirmation: new ToolConfirmationPolicy { Required = true, Purpose = "create-lead" },
            reliability: new ToolReliabilityPolicy { Timeout = TimeSpan.FromSeconds(5), MaxRetries = 2, RequiresIdempotencyKey = true },
            dataPolicy: new ToolDataPolicy { Sensitive = true, AuditCategory = "lead" });

        Assert.Equal(ToolSideEffect.Mutating, descriptor.SideEffect);
        Assert.Equal("lead.write", descriptor.Authorization.PolicyName);
        Assert.True(descriptor.Confirmation.Required);
        Assert.Equal(2, descriptor.Reliability.MaxRetries);
        Assert.True(descriptor.DataPolicy.Sensitive);
    }

    [Fact]
    public void Descriptor_RejectsInvalidSafetyMetadata()
    {
        Assert.Throws<ArgumentException>(() => new ToolDescriptor("tool", "1", "Tool", "Description",
            typeof(string), typeof(string), reliability: new ToolReliabilityPolicy { Timeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentException>(() => new ToolDescriptor("tool", "1", "Tool", "Description",
            typeof(string), typeof(string), confirmation: new ToolConfirmationPolicy { Required = true }));
    }
}
