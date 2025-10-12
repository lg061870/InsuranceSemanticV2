using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.Context;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.StateMachine;
using ConversaCore.Cards;
using ConversaCore.Models;

namespace InsuranceAgent.Topics.RadioButtonDemoTopic
{
    /// <summary>
    /// Demonstrates the validation of radio button groups
    /// with the enhanced RequiredChoiceAttribute.
    /// </summary>
    public class RadioButtonDemoTopic : TopicFlow
    {
        private readonly ILogger<RadioButtonDemoTopic> _logger;
        private readonly IConversationContext _conversationContext;
        
        private readonly TopicStateMachine<FlowState> _fsm;
        
        // Keywords used for intent matching
        public static readonly string[] IntentKeywords = new[]
        {
            "radio demo", "radio button demo", "test radio buttons", "validation demo", "choice validation"
        };
        
        public RadioButtonDemoTopic(
            TopicWorkflowContext workflowContext,
            ILogger<RadioButtonDemoTopic> logger,
            IConversationContext conversationContext) : base(workflowContext, logger, name: "RadioButtonDemoTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;
            
            // Initialize state machine
            _fsm = new TopicStateMachine<FlowState>(FlowState.Idle);
            _fsm.ConfigureTransition(FlowState.Idle, FlowState.Starting);
            _fsm.ConfigureTransition(FlowState.Starting, FlowState.PromptingForRadioButtons);
            _fsm.ConfigureTransition(FlowState.PromptingForRadioButtons, FlowState.ProcessingRadioButtonResponse);
            _fsm.ConfigureTransition(FlowState.ProcessingRadioButtonResponse, FlowState.PromptingForRadioButtons);
            _fsm.ConfigureTransition(FlowState.ProcessingRadioButtonResponse, FlowState.Complete);
            
            // Build activity queue
            Add(new SimpleActivity("Start", async (ctx, data) =>
            {
                await _fsm.TryTransitionAsync(FlowState.Starting);
                
                // Adding a message to the context
                return ActivityResult.Continue("Let's test radio button validation in adaptive cards.");
            }));
            
            // Add transition activity
            Add(new SimpleActivity("Transition", async (ctx, data) =>
            {
                await _fsm.TryTransitionAsync(FlowState.PromptingForRadioButtons);
                return ActivityResult.Continue();
            }));
            
            Add(new AdaptiveCardActivity<RadioButtonDemoCard, RadioButtonDemoModel>(
                "RadioButtonForm", 
                Context,
                // Card factory - creates the card with existing values if present
                cardFactory: (card) => 
                {
                    // Get the model from context if available
                    var model = Context.GetValue<RadioButtonDemoModel>("RadioButtonDemoModel");
                    
                    var cardObj = new RadioButtonDemoCard();
                    return cardObj.Create(
                        model?.HasInsurance,
                        model?.HasHome,
                        model?.HasCar
                    );
                },
                // Model context key for storing the model
                modelContextKey: "RadioButtonDemoModel",
                // Use null for the logger parameter
                logger: null,
                // On transition - replaces the incorrect onModelBound parameter
                onTransition: (from, to, data) =>
                {
                    if (from == ActivityState.InputCollected && to == ActivityState.Completed && data is RadioButtonDemoModel model)
                    {
                        // On valid submission
                        _fsm.TryTransitionAsync(FlowState.ProcessingRadioButtonResponse).Wait();
                        
                        // Store in conversation context for later use
                        _conversationContext.SetValue("hasInsurance", model.HasInsurance ?? false);
                        _conversationContext.SetValue("hasHome", model.HasHome ?? false);
                        _conversationContext.SetValue("hasCar", model.HasCar ?? false);
                        
                        // Update state
                        _fsm.TryTransitionAsync(FlowState.Complete).Wait();
                    }
                },
                // Add custom message
                customMessage: "Please answer all questions about your insurance status."
            ));
            
            // Add summary activity to show results after form is completed
            Add(new SimpleActivity("SummaryActivity", async (ctx, data) =>
            {
                if (_fsm.CurrentState == FlowState.Complete)
                {
                    // Retrieve values from conversation context
                    bool hasInsurance = _conversationContext.GetValue<bool>("hasInsurance");
                    bool hasHome = _conversationContext.GetValue<bool>("hasHome");
                    bool hasCar = _conversationContext.GetValue<bool>("hasCar");
                    
                    // Compose a summary message
                    string summaryMessage = $"Thank you for your responses!\n\n" +
                                          $"• Have insurance: {(hasInsurance ? "Yes" : "No")}\n" +
                                          $"• Own home: {(hasHome ? "Yes" : "No")}\n" +
                                          $"• Own car: {(hasCar ? "Yes" : "No")}\n";
                    
                    return ActivityResult.Continue(summaryMessage);
                }
                
                return ActivityResult.Continue();
            }));
        }
        
        public override async Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
        {
            foreach (var keyword in IntentKeywords)
            {
                if (message.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return 1.0f; // High confidence match
                }
            }
            
            return 0.0f; // No match
        }
        
        // States for this topic's state machine
        private new enum FlowState
        {
            Idle,
            Starting,
            PromptingForRadioButtons,
            ProcessingRadioButtonResponse,
            Complete
        }
    }
}