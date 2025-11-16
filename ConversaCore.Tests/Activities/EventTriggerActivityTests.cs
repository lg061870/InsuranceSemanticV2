using System;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Xunit;

namespace ConversaCore.Tests.Activities;

public class EventTriggerActivityTests {
    private readonly TopicWorkflowContext _context;
    private readonly ILogger _logger;

    public EventTriggerActivityTests() {
        _context = new TopicWorkflowContext();
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task FireAndForget_Should_Complete_Immediately() {
        // Arrange
        var eventFired = false;
        string? eventName = null;
        string? eventData = null;

        var activity = new EventTriggerActivity(
            id: "TestFireForget",
            eventName: "test.event",
            eventData: "test data",
            waitForResponse: false,
            logger: _logger
        );

        activity.CustomEventTriggered += (sender, e) => {
            eventFired = true;
            eventName = e.EventName;
            eventData = e.EventData?.ToString();
        };

        // Act
        var result = await activity.RunAsync(_context);

        // Assert
        result.IsWaiting.Should().BeFalse();
        result.IsEnd.Should().BeFalse();
        result.Message.Should().NotBeNull();
        eventFired.Should().BeTrue();
        eventName.Should().Be("test.event");
        eventData.Should().Contain("test data");
        activity.CurrentState.Should().Be(ActivityState.Completed);
    }

    [Fact]
    public async Task WaitForResponse_Should_Wait_For_UI_Response() {
        // Arrange
        var eventFired = false;
        var activity = EventTriggerActivity.CreateWaitForResponse(
            id: "TestWaitForResponse",
            eventName: "test.wait.event",
            responseContextKey: "wait_response",
            eventData: new { message = "Please respond" },
            responseTimeout: TimeSpan.FromMinutes(1), // Provide valid timeout
            logger: _logger
        );

        activity.CustomEventTriggered += (sender, e) => eventFired = true;

        // Act
        var result = await activity.RunAsync(_context);

        // Assert
        result.IsWaiting.Should().BeTrue();
        eventFired.Should().BeTrue();
        activity.CurrentState.Should().Be(ActivityState.WaitingForUserInput);
        _context.GetValue<string>($"{activity.Id}_WaitingForEvent").Should().Be("test.wait.event");
    }

    [Fact]
    public async Task HandleUIResponse_Should_Complete_Activity() {
        // Arrange
        var activity = EventTriggerActivity.CreateWaitForResponse(
            id: "TestHandleResponse",
            eventName: "test.response.event",
            responseContextKey: "user_response",
            eventData: null,
            responseTimeout: TimeSpan.FromMinutes(1), // Provide valid timeout
            logger: _logger
        );

        // Setup event handler to prevent Failed state
        activity.CustomEventTriggered += (sender, e) => {
            // Event handler to allow proper flow
        };

        // First trigger to put into waiting state
        await activity.RunAsync(_context);
        activity.CurrentState.Should().Be(ActivityState.WaitingForUserInput);

        // Act - simulate UI response
        var responseData = new { choice = "yes", comments = "Looks good!" };
        activity.HandleUIResponse(_context, responseData);

        // Assert
        activity.CurrentState.Should().Be(ActivityState.Completed);
        _context.GetValue<object>("user_response").Should().Be(responseData);

        // Verify markers cleared
        _context.GetValue<string>($"{activity.Id}_WaitingForEvent").Should().BeNull();
        _context.GetValue<string>($"{activity.Id}_ResponseKey").Should().BeNull();
    }

    [Fact]
    public void Constructor_Should_Validate_Parameters() {
        // Test null/empty event name
        var ex1 = Assert.Throws<ArgumentException>(() =>
            new EventTriggerActivity("test", "", null, false, null, null, _logger));
        ex1.ParamName.Should().Be("eventName");

        var ex2 = Assert.Throws<ArgumentException>(() =>
            new EventTriggerActivity("test", null!, null, false, null, null, _logger));
        ex2.ParamName.Should().Be("eventName");

        // Test missing response key for blocking events
        var ex3 = Assert.Throws<ArgumentException>(() =>
            new EventTriggerActivity("test", "event", null, true, null, null, _logger));
        ex3.ParamName.Should().Be("responseContextKey");

        var ex4 = Assert.Throws<ArgumentException>(() =>
            new EventTriggerActivity("test", "event", null, true, "", null, _logger));
        ex4.ParamName.Should().Be("responseContextKey");
    }

    [Fact]
    public async Task GetWaitingInfo_Should_Return_Correct_Information() {
        // Arrange
        var activity = EventTriggerActivity.CreateWaitForResponse(
            id: "TestWaitInfo",
            eventName: "test.waiting.event",
            responseContextKey: "response_key",
            eventData: null,
            responseTimeout: TimeSpan.FromMinutes(1), // Provide valid timeout
            logger: _logger
        );

        // Setup event handler to prevent Failed state
        activity.CustomEventTriggered += (sender, e) => {
            // Event handler to allow proper flow
        };

        // Before running
        var (eventName, responseKey, isWaiting) = activity.GetWaitingInfo();
        eventName.Should().Be("test.waiting.event");
        responseKey.Should().Be("response_key");
        isWaiting.Should().BeFalse();

        // After running
        await activity.RunAsync(_context);
        var (_, _, isWaiting2) = activity.GetWaitingInfo();
        isWaiting2.Should().BeTrue();
    }

    [Fact]
    public void ToString_Should_Return_Descriptive_String() {
        // Arrange
        var fireForget = new EventTriggerActivity(
            id: "FireForgetTest",
            eventName: "test.event",
            eventData: null,
            waitForResponse: false,
            logger: _logger
        );

        var waitResponse = new EventTriggerActivity(
            id: "WaitTest",
            eventName: "test.wait.event",
            eventData: null,
            waitForResponse: true,
            responseContextKey: "response",
            logger: _logger
        );

        // Act & Assert
        fireForget.ToString().Should().Be("EventTriggerActivity(FireForgetTest: test.event, FireAndForget)");
        waitResponse.ToString().Should().Be("EventTriggerActivity(WaitTest: test.wait.event, WaitForResponse)");
    }
}
