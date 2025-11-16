//using ConversaCore.Context;
//using ConversaCore.Events;
//using ConversaCore.Services;
//using ConversaCore.Topics;
//using FluentAssertions;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Microsoft.SemanticKernel;
//using Moq;
//using System;
//using System.Collections.Generic;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;

//namespace ConversaCore.Tests
//{
//    // Make sure class is public for test discovery
//    public class IntegrationResetTests
//    {
//        [Fact]
//        public async Task ChatService_ResetAsync_ShouldResetAllComponents()
//        {
//            try
//            {
//                Console.WriteLine("Starting test ChatService_ResetAsync_ShouldResetAllComponents");
//                // Arrange
//                var loggerMock = new Mock<ILogger<ChatService>>();
//                var contextMock = new Mock<IConversationContext>();
//                // Create a real kernel for testing
//                var kernel = Kernel.CreateBuilder().Build();
//                var topicRegistryMock = new Mock<TopicRegistry>(new Mock<ILogger<TopicRegistry>>().Object);
//                Console.WriteLine("Created mocks");
                
//                // We can't verify Reset() since it's not virtual, so we'll just verify TerminateAsync
//                contextMock.Setup(x => x.TerminateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
//                contextMock.Setup(x => x.ConversationId).Returns("test-conversation-id");
//                Console.WriteLine("Setup expectations");
                
//                var chatService = new ChatService(
//                    kernel,
//                    topicRegistryMock.Object,
//                    contextMock.Object,
//                    loggerMock.Object);
//                Console.WriteLine("Created chat service");
                
//                // Act
//                Console.WriteLine("Executing ResetAsync...");
//                var result = await chatService.ResetAsync();
//                Console.WriteLine($"Reset result: {result}");
                
//                // Assert
//                Console.WriteLine("Running assertions...");
//                contextMock.Verify(x => x.TerminateAsync(It.IsAny<CancellationToken>()), Times.Once);
//                Console.WriteLine("TerminateAsync verified");
                
//                // Since Reset() is not virtual, we can't verify it directly
//                // Use SafeAny to handle nullability differences
//                loggerMock.Verify(x => x.Log(
//                    It.IsAny<LogLevel>(),
//                    It.IsAny<EventId>(),
//                    It.IsAny<It.IsAnyType>(),
//                    It.IsAny<Exception>(),
//                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
//                    Times.AtLeastOnce);
//                Console.WriteLine("Logger verification complete");
//                Console.WriteLine("Test completed successfully");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Test exception: {ex.Message}");
//                Console.WriteLine(ex.StackTrace);
//                throw;
//            }
//        }
        
//        [Fact]
//        public async Task ResetConversation_ShouldPublishEventAndClearState()
//        {
//            try
//            {
//                // Arrange
//                Console.WriteLine("Test starting...");
//                var services = new ServiceCollection();
//                services.AddLogging();
                
//                // Create mocks for our components
//                var contextMock = new Mock<IConversationContext>();
//                var topicRegistryMock = new Mock<TopicRegistry>(new Mock<ILogger<TopicRegistry>>().Object);
//                // Create a real kernel for testing
//                var kernel = Kernel.CreateBuilder().Build();
                
//                // Setup expectations
//                contextMock.Setup(x => x.TerminateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
//                contextMock.Setup(x => x.ConversationId).Returns("test-conversation-id");
//                Console.WriteLine("Mocks set up...");
                
//                // We can't create a new instance since the constructor is private
//                // Just make sure the current instance is terminated and clean
//                try
//                {
//                    TopicEventBus.Instance.Terminate();
//                    Console.WriteLine("Terminated existing event bus...");
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Error terminating event bus: {ex.Message}");
//                }
                
//                // Flag to track if event was received
//                bool eventReceived = false;
//                string receivedEventType = string.Empty;
                
//                // Subscribe to the event
//                Console.WriteLine("Subscribing to event...");
//                TopicEventBus.Instance.Subscribe(TopicEventType.ConversationReset, (e) => {
//                    Console.WriteLine($"Event received: {e.EventType}");
//                    eventReceived = true;
//                    receivedEventType = e.EventType.ToString();
//                    return Task.CompletedTask;
//                });
                
//                // Register services
//                Console.WriteLine("Registering services...");
//                services.AddSingleton(contextMock.Object);
//                services.AddSingleton(kernel);
//                services.AddSingleton(topicRegistryMock.Object);
//                services.AddScoped<IChatService, ChatService>();
                
//                var serviceProvider = services.BuildServiceProvider();
                
//                // Act
//                Console.WriteLine("Getting chat service...");
//                var chatService = serviceProvider.GetRequiredService<IChatService>();
//                Console.WriteLine("Running reset...");
//                var result = await chatService.ResetAsync();
//                Console.WriteLine($"Reset result: {result}");
                
//                // Wait a short time for async events to complete
//                await Task.Delay(100);
                
//                // Assert
//                Console.WriteLine("Running assertions...");
//                contextMock.Verify(x => x.TerminateAsync(It.IsAny<CancellationToken>()), Times.Once);
//                Console.WriteLine("TerminateAsync verified");
                
//                // Check event receipt
//                if (result)
//                {
//                    Console.WriteLine($"Event received: {eventReceived}, Type: {receivedEventType}");
//                    Assert.True(eventReceived, $"ConversationReset event was not received.");
//                    Assert.Equal(TopicEventType.ConversationReset.ToString(), receivedEventType);
//                }
                
//                Console.WriteLine("Test completed successfully");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Test exception: {ex.Message}");
//                Console.WriteLine(ex.StackTrace);
//                throw;
//            }
//            finally
//            {
//                // Always clean up
//                try
//                {
//                    TopicEventBus.Instance.Terminate();
//                    Console.WriteLine("Event bus terminated.");
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Cleanup error: {ex.Message}");
//                }
//            }
//        }
//    }
//}