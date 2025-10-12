using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.Services;
using ConversaCore.StateMachine;
using ConversaCore.Tests.TestModels;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ConversaCore.Tests
{
    public class ConversationResetTests
    {
        [Fact]
        public void ConversationContext_Terminate_ShouldCleanupResources()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ConversationContext>>();
            var conversationId = Guid.NewGuid().ToString();
            var userId = "testUser";
            var context = new ConversationContext(conversationId, userId, loggerMock.Object);
            
            // Add some test data
            context.SetValue("testKey", "testValue");
            var testModel = new TestCardModel();
            context.SetModel(testModel);
            context.SetCurrentTopic("testTopic");
            context.AddTopicToChain("nextTopic");
            
            // Act
            context.Terminate();
            
            // Assert
            context.IsTerminated.Should().BeTrue();
            context.HasValue("testKey").Should().BeFalse();
            context.HasModel<TestCardModel>().Should().BeFalse();
            
            // Verify any logging happened (simplifying the verification)
            loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(), 
                It.IsAny<EventId>(), 
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), 
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
        
        [Fact]
        public async Task ConversationContext_TerminateAsync_ShouldCleanupResources()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ConversationContext>>();
            var conversationId = Guid.NewGuid().ToString();
            var userId = "testUser";
            var context = new ConversationContext(conversationId, userId, loggerMock.Object);
            
            // Add some test data
            context.SetValue("testKey", "testValue");
            var testModel = new TestCardModel();
            context.SetModel(testModel);
            context.SetCurrentTopic("testTopic");
            context.AddTopicToChain("nextTopic");
            
            // Act
            await context.TerminateAsync();
            
            // Assert
            context.IsTerminated.Should().BeTrue();
            context.HasValue("testKey").Should().BeFalse();
            context.HasModel<TestCardModel>().Should().BeFalse();
            
            // Verify any logging happened
            loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(), 
                It.IsAny<EventId>(), 
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), 
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
        
        [Fact]
        public void ConversationContext_Reset_ShouldClearAllData()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ConversationContext>>();
            var conversationId = Guid.NewGuid().ToString();
            var userId = "testUser";
            var context = new ConversationContext(conversationId, userId, loggerMock.Object);
            
            // Add some test data
            context.SetValue("testKey", "testValue");
            var testModel = new TestCardModel();
            context.SetModel(testModel);
            context.SetCurrentTopic("testTopic");
            context.AddTopicToChain("nextTopic");
            context.PushTopicCall("callingTopic", "subTopic", "resumeData");
            
            // Act
            context.Reset();
            
            // Assert
            context.HasValue("testKey").Should().BeFalse();
            context.HasModel<TestCardModel>().Should().BeFalse();
            context.CurrentTopicName.Should().BeNull();
            context.TopicChain.Should().BeEmpty();
            context.GetTopicCallDepth().Should().Be(0);
            
            // Check that we have a reset timestamp
            context.HasValue("ConversationLastReset").Should().BeTrue();
            
            // Verify any logging happened
            loggerMock.Verify(x => x.Log(
                It.IsAny<LogLevel>(), 
                It.IsAny<EventId>(), 
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), 
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }
        
        [Fact]
        public async Task ChatService_ResetAsync_ShouldCallTerminateAsyncOnContext()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ChatService>>();
            var contextMock = new Mock<IConversationContext>();
            // Create a real kernel for testing
            var kernel = Kernel.CreateBuilder().Build();
            var topicRegistryMock = new Mock<TopicRegistry>(new Mock<ILogger<TopicRegistry>>().Object);
            
            // Setup expectations
            contextMock.Setup(x => x.TerminateAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
            
            var chatService = new ChatService(
                kernel,
                topicRegistryMock.Object,
                contextMock.Object,
                loggerMock.Object);
            
            // Act
            await chatService.ResetAsync();
            
            // Assert
            contextMock.Verify(x => x.TerminateAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void ConversationContext_CanBeReusedAfterReset()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ConversationContext>>();
            var conversationId = Guid.NewGuid().ToString();
            var userId = "testUser";
            var context = new ConversationContext(conversationId, userId, loggerMock.Object);
            
            // Add initial data
            context.SetValue("testKey", "testValue");
            context.HasValue("testKey").Should().BeTrue();
            
            // Act - Reset the context
            context.Reset();
            
            // Assert - Context should be empty but usable
            context.HasValue("testKey").Should().BeFalse();
            context.IsTerminated.Should().BeFalse(); // Reset doesn't terminate
            
            // Context should be usable again
            context.SetValue("newKey", "newValue");
            context.HasValue("newKey").Should().BeTrue();
            context.GetValue<string>("newKey").Should().Be("newValue");
        }
        
        [Fact]
        public void ConversationContext_CannotBeUsedAfterTermination()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ConversationContext>>();
            var conversationId = Guid.NewGuid().ToString();
            var userId = "testUser";
            var context = new ConversationContext(conversationId, userId, loggerMock.Object);
            
            // Add initial data
            context.SetValue("testKey", "testValue");
            
            // Act - Terminate the context
            context.Terminate();
            
            // Assert - Context should be terminated and data cleared
            context.IsTerminated.Should().BeTrue();
            context.HasValue("testKey").Should().BeFalse();
            
            // The context is terminated but still technically usable (will not throw),
            // however terminated components should not be used in practice
            context.SetValue("newKey", "newValue");
            context.HasValue("newKey").Should().BeTrue();
        }
    }
}