using System;
using ConversaCore.Runtime;
using FluentAssertions;
using Xunit;

namespace ConversaCore.Tests.Runtime;

/// <summary>
/// Coverage for <see cref="HostInteractionResponse"/>: construction with valid data,
/// rejection of an invalid request ID, and the optional/nullable payload behavior.
/// </summary>
public class HostInteractionResponseTests
{
    [Fact]
    public void Constructor_WithValidRequestIdAndPayload_SetsRequestIdAndPayload()
    {
        var response = new HostInteractionResponse("req-123", payload: "confirmed");

        response.RequestId.Should().Be("req-123");
        response.Payload.Should().Be("confirmed");
    }

    [Fact]
    public void Constructor_WithoutPayload_DefaultsPayloadToNull()
    {
        var response = new HostInteractionResponse("req-123");

        response.Payload.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullEmptyOrWhitespaceRequestId_ThrowsArgumentException(string? invalidRequestId)
    {
        Action act = () => new HostInteractionResponse(invalidRequestId!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("requestId");
    }
}
