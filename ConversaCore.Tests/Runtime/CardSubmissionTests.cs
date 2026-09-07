using System;
using System.Collections.Generic;
using ConversaCore.Runtime;
using FluentAssertions;
using Xunit;

namespace ConversaCore.Tests.Runtime;

/// <summary>
/// Coverage for <see cref="CardSubmission"/>: construction with valid data, rejection of
/// an invalid card ID, and the default-to-empty-dictionary behavior when no data is
/// supplied.
/// </summary>
public class CardSubmissionTests
{
    [Fact]
    public void Constructor_WithValidCardIdAndData_SetsCardIdAndData()
    {
        var data = new Dictionary<string, object> { ["firstName"] = "Ada" };

        var submission = new CardSubmission("compliance-card", data);

        submission.CardId.Should().Be("compliance-card");
        submission.Data.Should().BeSameAs(data);
    }

    [Fact]
    public void Constructor_WithoutData_DefaultsToEmptyDictionary()
    {
        var submission = new CardSubmission("compliance-card");

        submission.Data.Should().NotBeNull();
        submission.Data.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullData_DefaultsToEmptyDictionary()
    {
        var submission = new CardSubmission("compliance-card", data: null);

        submission.Data.Should().NotBeNull();
        submission.Data.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullEmptyOrWhitespaceCardId_ThrowsArgumentException(string? invalidCardId)
    {
        Action act = () => new CardSubmission(invalidCardId!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("cardId");
    }
}
