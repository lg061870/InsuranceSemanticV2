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
        var eventName = "";
        var eventData = "";

        var activity = EventTriggerActivity.CreateFireAndForget(
            "TestFireForget",
            "test.event",
            "test data",
            _logger
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
        eventData.Should().Be("test data");
        activity.CurrentState.Should().Be(ActivityState.Completed);
    }

    [Fact]
    public async Task WaitForResponse_Should_Wait_For_UI_Response() {
        // Arrange
        var eventFired = false;
        var activity = EventTriggerActivity.CreateWaitForResponse(
            "TestWaitForResponse",
            "test.wait.event",
            "test_response",
            new { question = "Do you agree?" },
            _logger
        );

        activity.CustomEventTriggered += (sender, e) => {
            eventFired = true;
        };

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
            "TestHandleResponse",
            "test.response.event",
            "user_response",
            null,
            _logger
        );

        // First trigger the event to put it in waiting state
        await activity.RunAsync(_context);
        activity.CurrentState.Should().Be(ActivityState.WaitingForUserInput);

        // Act - simulate UI response
        var responseData = new { choice = "yes", comments = "Looks good!" };
        activity.HandleUIResponse(_context, responseData);

        // Assert
        activity.CurrentState.Should().Be(ActivityState.Completed);
        _context.GetValue<object>("user_response").Should().Be(responseData);
        
        // Check that waiting markers are cleaned up
        var waitingEvent = _context.GetValue<string>($"{activity.Id}_WaitingForEvent");
        waitingEvent.Should().BeNull();
    }

    [Fact]
    public void Constructor_Should_Validate_Parameters() {
        // Test null/empty event name
        var ex1 = Assert.Throws<ArgumentException>(() => 
            new EventTriggerActivity("test", "", null, false, null, _logger));
        ex1.ParamName.Should().Be("eventName");
        
        var ex2 = Assert.Throws<ArgumentException>(() => 
            new EventTriggerActivity("test", null!, null, false, null, _logger));
        ex2.ParamName.Should().Be("eventName");

        // Test missing response key for blocking events
        var ex3 = Assert.Throws<ArgumentException>(() => 
            new EventTriggerActivity("test", "event", null, true, null, _logger));
        ex3.ParamName.Should().Be("responseContextKey");
        
        var ex4 = Assert.Throws<ArgumentException>(() => 
            new EventTriggerActivity("test", "event", null, true, "", _logger));
        ex4.ParamName.Should().Be("responseContextKey");
    }

    [Fact]
    public async Task GetWaitingInfo_Should_Return_Correct_Information() {
        // Arrange
        var activity = EventTriggerActivity.CreateWaitForResponse(
            "TestWaitInfo",
            "test.waiting.event",
            "response_key",
            null,
            _logger
        );

        // Act
        var (eventName, responseKey, isWaiting) = activity.GetWaitingInfo();

        // Assert
        eventName.Should().Be("test.waiting.event");
        responseKey.Should().Be("response_key");
        isWaiting.Should().BeFalse(); // Not waiting until RunAsync is called

        // After running, should be waiting
        await activity.RunAsync(_context);
        var (eventName2, responseKey2, isWaiting2) = activity.GetWaitingInfo();
        isWaiting2.Should().BeTrue();
    }

    [Fact]
    public void ToString_Should_Return_Descriptive_String() {
        // Arrange
        var fireForgetActivity = EventTriggerActivity.CreateFireAndForget(
            "FireForgetTest",
            "test.event",
            null,
            _logger
        );

        var waitForResponseActivity = EventTriggerActivity.CreateWaitForResponse(
            "WaitTest",
            "test.wait.event",
            "response",
            null,
            _logger
        );

        // Act & Assert
        fireForgetActivity.ToString().Should()
            .Be("EventTriggerActivity(FireForgetTest: test.event, FireAndForget)");
            
        waitForResponseActivity.ToString().Should()
            .Be("EventTriggerActivity(WaitTest: test.wait.event, WaitForResponse)");
    }
}