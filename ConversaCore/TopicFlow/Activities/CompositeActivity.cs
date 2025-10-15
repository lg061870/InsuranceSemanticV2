using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.TopicFlow.Activities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.TopicFlow {
    /// <summary>
    /// An activity that contains and orchestrates a sequence of child activities.
    /// </summary>
    public class CompositeActivity : TopicFlowActivity, IAdaptiveCardActivity, ITopicTriggeredActivity, ICustomEventTriggeredActivity {
        private readonly IList<TopicFlowActivity> _activities;
        private TopicFlowActivity? _currentWaitingActivity; // Track which child activity is waiting for input
        private TopicWorkflowContext? _storedContext; // Store context for continued execution

        /// <summary>
        /// Event fired when child TriggerTopicActivity triggers a topic
        /// </summary>
        public event EventHandler<TopicTriggeredEventArgs>? TopicTriggered;

        /// <summary>
        /// Event fired when child EventTriggerActivity triggers a custom event
        /// </summary>
        public event EventHandler<CustomEventTriggeredEventArgs>? CustomEventTriggered;

        /// <summary>
        /// CompositeActivity forwards the WaitForCompletion behavior from its child activities
        /// </summary>
        public bool WaitForCompletion => _currentWaitingActivity is ITopicTriggeredActivity triggeredActivity ? triggeredActivity.WaitForCompletion : false;

        // Forward child activity lifecycle and completion events
        public new event EventHandler<ActivityLifecycleEventArgs>? ActivityLifecycleChanged;
        public new event EventHandler<ActivityCompletedEventArgs>? ActivityCompleted;

        public event EventHandler<CardJsonEventArgs>? CardJsonEmitted;
        public event EventHandler<CardJsonEventArgs>? CardJsonSending;
        public event EventHandler<CardJsonEventArgs>? CardJsonSent;
        public event EventHandler<CardJsonRenderedEventArgs>? CardJsonRendered;
        public event EventHandler<CardDataReceivedEventArgs>? CardDataReceived;
        public event EventHandler<ModelBoundEventArgs>? ModelBound;
        public event EventHandler<ValidationFailedEventArgs>? ValidationFailed;

        /// <summary>
        /// Gets a value indicating whether the context of this composite activity is isolated.
        /// When true, changes to the context within this activity will not affect the parent context.
        /// </summary>
        public bool IsolateContext { get; set; }

        /// <summary>
        /// Gets or sets a message to display when all activities have completed.
        /// </summary>
        public string? CompleteMessage { get; set; }

        public CompositeActivity(string id, IEnumerable<TopicFlowActivity> activities)
            : base(id) {
            if (activities == null || !activities.Any())
                throw new ArgumentNullException(nameof(activities), "At least one activity must be provided.");

            _activities = new List<TopicFlowActivity>(activities);
            
            // Hook events for all child activities
            foreach (var activity in _activities) {
                HookChildEvents(activity);
            }
        }

        public static CompositeActivity Create(string id, params TopicFlowActivity[] activities)
            => new CompositeActivity(id, activities);

        public static CompositeActivity Create(string id, params Action<TopicWorkflowContext>[] actions) {
            if (actions == null || actions.Length == 0)
                throw new ArgumentNullException(nameof(actions), "At least one action must be provided.");

            var activities = new List<TopicFlowActivity>();
            for (int i = 0; i < actions.Length; i++) {
                var action = actions[i] ?? throw new ArgumentNullException(nameof(actions), $"Action at index {i} is null.");
                activities.Add(SimpleActivity.Create($"{id}_Step{i + 1}", action));
            }

            return new CompositeActivity(id, activities);
        }

        /// <inheritdoc/>
        protected override async Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default) {
            
            // Store context for potential continuation
            _storedContext = context;
            
            string currentIndexKey = $"{Id}_CurrentActivityIndex";
            int currentIndex = context.GetValue<int>(currentIndexKey, 0);
            
            Console.WriteLine($"[CompositeActivity] RunActivity called - currentIndex: {currentIndex}, input: {input?.GetType().Name ?? "null"}, activityCount: {_activities.Count}");

            // Setup working context (possibly isolated)
            TopicWorkflowContext workingContext = context;
            if (IsolateContext) {
                var isolatedKey = $"{Id}_IsolatedContext";
                workingContext = context.GetValue<TopicWorkflowContext>(isolatedKey)
                                 ?? new TopicWorkflowContext();

                if (!context.ContainsKey(isolatedKey))
                    context.SetValue(isolatedKey, workingContext);
            }

            // If input is passed, forward it to the current child activity
            if (input != null && currentIndex < _activities.Count) {
                var child = _activities[currentIndex];
                var result = await child.RunAsync(workingContext, input, cancellationToken);

                if (result.IsWaiting) {
                    _currentWaitingActivity = child; // Track which activity is waiting
                    return result;
                }

                currentIndex++;
                context.SetValue(currentIndexKey, currentIndex);
            }

            // Execute sequentially until done or waiting
            while (currentIndex < _activities.Count) {
                var child = _activities[currentIndex];
                Console.WriteLine($"[CompositeActivity] Executing child activity at index {currentIndex}: {child.Id}");
                var result = await child.RunAsync(workingContext, null, cancellationToken);

                if (result.IsWaiting) {
                    _currentWaitingActivity = child; // Track which activity is waiting
                    Console.WriteLine($"[CompositeActivity] Child activity {child.Id} is waiting");
                    return result;
                }

                // Child completed - clear waiting reference if it was this child
                if (_currentWaitingActivity == child) {
                    _currentWaitingActivity = null;
                }

                Console.WriteLine($"[CompositeActivity] Child activity {child.Id} completed, advancing to next");
                currentIndex++;
                context.SetValue(currentIndexKey, currentIndex);
            }

            // Reset index for future runs
            context.SetValue(currentIndexKey, 0);

            // Copy isolated outputs back to parent context
            if (IsolateContext)
                context.SetValue(Id, workingContext);

            // Completion message (optional)
            if (!string.IsNullOrEmpty(CompleteMessage))
                return ActivityResult.Continue(CompleteMessage);

            // Otherwise, just advance to the next activity in the queue
            return ActivityResult.Continue();

        }

        public void OnInputCollected(AdaptiveCardInputCollectedEventArgs e) {
            // Forward input to the activity that is currently waiting for input
            if (_currentWaitingActivity is IAdaptiveCardActivity cardActivity) {
                cardActivity.OnInputCollected(e);
                
                // Check if the activity completed after input
                if (_currentWaitingActivity.CurrentState == ActivityState.Completed) {
                    var previousWaitingActivity = _currentWaitingActivity;
                    _currentWaitingActivity = null;
                    
                    Console.WriteLine($"[CompositeActivity] Child activity '{previousWaitingActivity.Id}' completed, advancing index");
                    
                    // Advance the current index since this activity completed via input
                    if (_storedContext != null) {
                        string currentIndexKey = $"{Id}_CurrentActivityIndex";
                        int currentIndex = _storedContext.GetValue<int>(currentIndexKey, 0);
                        currentIndex++;
                        _storedContext.SetValue(currentIndexKey, currentIndex);
                        Console.WriteLine($"[CompositeActivity] Advanced index to {currentIndex}");
                        
                        // Continue execution of remaining activities
                        Console.WriteLine($"[CompositeActivity] Triggering continuation to execute remaining activities");
                        _ = Task.Run(async () => await ContinueExecutionAsync(_storedContext));
                    }
                    
                    return;
                }
                
                // Only clear if the activity is no longer waiting (completed or failed permanently)
                // If validation fails, the activity goes back to WaitingForUserInput, so keep the reference
                if (_currentWaitingActivity.CurrentState != ActivityState.WaitingForUserInput) {
                    _currentWaitingActivity = null;
                }
            }
        }

        /// <summary>
        /// Hooks into child activity events to enable event bubbling up to parent containers.
        /// This ensures that events from nested activities reach the InsuranceAgentService.
        /// 
        /// EVENT BUBBLING CHAIN:
        /// Child Activity (TriggerTopicActivity/AdaptiveCardActivity) 
        ///     ↓ fires event
        /// CompositeActivity.HookChildEvents() 
        ///     ↓ forwards via event handlers  
        /// CompositeActivity events (TopicTriggered/CardJsonSent/etc)
        ///     ↓ bubbled up to parent
        /// ConditionalActivity (if CompositeActivity is nested)
        ///     ↓ forwards to its parent
        /// InsuranceAgentService.HookTopicEvents() 
        ///     ↓ receives final events
        /// InsuranceAgentService event handlers (OnTopicTriggered/OnCardJsonSent/etc)
        /// </summary>
        /// <param name="child">The child activity to hook events for</param>
        private void HookChildEvents(TopicFlowActivity child) {
            if (child == null) return;

            Console.WriteLine($"[CompositeActivity.HookChildEvents] Subscribing to events from child activity '{child.Id}' (type: {child.GetType().Name}) within CompositeActivity '{this.Id}'");

            // Always hook basic lifecycle events
            child.ActivityLifecycleChanged += OnChildActivityLifecycleChanged;
            child.ActivityCompleted += OnChildActivityCompleted;

            // Hook adaptive card events if the child supports them
            if (child is IAdaptiveCardActivity cardChild) {
                cardChild.CardJsonEmitted += (sender, e) => {
                    CardJsonEmitted?.Invoke(sender, e);
                };

                cardChild.CardJsonSending += (sender, e) => {
                    CardJsonSending?.Invoke(sender, e);
                };

                cardChild.CardJsonSent += (sender, e) => {
                    CardJsonSent?.Invoke(sender, e);
                };

                cardChild.CardJsonRendered += (sender, e) => {
                    CardJsonRendered?.Invoke(sender, e);
                };

                cardChild.CardDataReceived += (sender, e) => {
                    CardDataReceived?.Invoke(sender, e);
                };

                cardChild.ModelBound += (sender, e) => {
                    ModelBound?.Invoke(sender, e);
                };

                cardChild.ValidationFailed += (sender, e) => {
                    ValidationFailed?.Invoke(sender, e);
                };
            }

            // Hook topic trigger events if the child can trigger topics
            // EVENT BUBBLING: TriggerTopicActivity.TopicTriggered → CompositeActivity.TopicTriggered → Parent Container
            if (child is ITopicTriggeredActivity topicTriggeredChild) {
                Console.WriteLine($"[CompositeActivity.HookChildEvents] Child '{child.Id}' implements ITopicTriggeredActivity - subscribing to TopicTriggered events");
                topicTriggeredChild.TopicTriggered += (sender, e) => {
                    var senderType = sender?.GetType().Name ?? "Unknown";
                    var senderId = sender is TopicFlowActivity activity ? activity.Id : "Unknown";
                    
                    Console.WriteLine($"[CompositeActivity.TopicTriggered] *** EVENT BUBBLE *** Forwarding TopicTriggered from child '{senderId}' ({senderType}) to parent: Topic='{e.TopicName}', CompositeActivity='{this.Id}'");
                    
                    // CRITICAL: Forward event to parent - this should reach InsuranceAgentService if properly subscribed
                    Console.WriteLine($"[CompositeActivity.TopicTriggered] About to forward TopicTriggered event to parent. Subscribers: {TopicTriggered?.GetInvocationList().Length ?? 0}");
                    TopicTriggered?.Invoke(sender, e);
                    Console.WriteLine($"[CompositeActivity.TopicTriggered] TopicTriggered event forwarded to parent");
                };
            }

            // Hook custom event trigger events if the child can trigger custom events  
            // EVENT BUBBLING: EventTriggerActivity.CustomEventTriggered → CompositeActivity.CustomEventTriggered → Parent Container
            if (child is ICustomEventTriggeredActivity customEventTriggeredChild) {
                Console.WriteLine($"[CompositeActivity.HookChildEvents] Child '{child.Id}' implements ICustomEventTriggeredActivity - subscribing to CustomEventTriggered events");
                customEventTriggeredChild.CustomEventTriggered += (sender, e) => {
                    var senderType = sender?.GetType().Name ?? "Unknown";
                    var senderId = sender is TopicFlowActivity activity ? activity.Id : "Unknown";
                    
                    Console.WriteLine($"[CompositeActivity.CustomEventTriggered] *** EVENT BUBBLE *** Forwarding CustomEventTriggered from child '{senderId}' ({senderType}) to parent: EventName='{e.EventName}', CompositeActivity='{this.Id}'");
                    
                    // CRITICAL: Forward event to parent - this should reach InsuranceAgentService if properly subscribed
                    Console.WriteLine($"[CompositeActivity.CustomEventTriggered] About to forward CustomEventTriggered event to parent. Subscribers: {CustomEventTriggered?.GetInvocationList().Length ?? 0}");
                    CustomEventTriggered?.Invoke(sender, e);
                    Console.WriteLine($"[CompositeActivity.CustomEventTriggered] CustomEventTriggered event forwarded to parent");
                };
            }

            // Future: Hook other specialized event interfaces as they are added
            // if (child is ISemanticEventsPrompt semanticChild) { ... }
            // if (child is IWorkflowEventActivity workflowChild) { ... }
        }

        /// <summary>
        /// Forward child lifecycle events by bubbling them up through the CompositeActivity
        /// </summary>
        private void OnChildActivityLifecycleChanged(object? sender, ActivityLifecycleEventArgs e) {
            // Bubble up the child's lifecycle event unchanged - this is event forwarding
            // The InsuranceAgentService subscribes to CompositeActivity's events and will receive this
            ActivityLifecycleChanged?.Invoke(sender, e);
        }

        /// <summary>
        /// Forward child completion events by bubbling them up through the CompositeActivity
        /// </summary>
        private void OnChildActivityCompleted(object? sender, ActivityCompletedEventArgs e) {
            // Bubble up the child's completion event unchanged - this is event forwarding  
            // The InsuranceAgentService subscribes to CompositeActivity's events and will receive this
            ActivityCompleted?.Invoke(sender, e);
        }

        /// <summary>
        /// Continue execution of remaining activities after input collection
        /// </summary>
        private async Task ContinueExecutionAsync(TopicWorkflowContext context) {
            try {
                Console.WriteLine($"[CompositeActivity] ContinueExecutionAsync started");
                
                string currentIndexKey = $"{Id}_CurrentActivityIndex";
                int currentIndex = context.GetValue<int>(currentIndexKey, 0);
                
                // Setup working context (possibly isolated)
                TopicWorkflowContext workingContext = context;
                if (IsolateContext) {
                    var isolatedKey = $"{Id}_IsolatedContext";
                    workingContext = context.GetValue<TopicWorkflowContext>(isolatedKey)
                                     ?? new TopicWorkflowContext();

                    if (!context.ContainsKey(isolatedKey))
                        context.SetValue(isolatedKey, workingContext);
                }
                
                // Continue executing remaining activities
                while (currentIndex < _activities.Count) {
                    var child = _activities[currentIndex];
                    Console.WriteLine($"[CompositeActivity] Continuing execution - child activity at index {currentIndex}: {child.Id}");
                    var result = await child.RunAsync(workingContext, null, CancellationToken.None);

                    if (result.IsWaiting) {
                        _currentWaitingActivity = child; // Track which activity is waiting
                        Console.WriteLine($"[CompositeActivity] Child activity {child.Id} is waiting - continuation paused");
                        return;
                    }

                    // Child completed - clear waiting reference if it was this child
                    if (_currentWaitingActivity == child) {
                        _currentWaitingActivity = null;
                    }

                    Console.WriteLine($"[CompositeActivity] Child activity {child.Id} completed during continuation, advancing to next");
                    currentIndex++;
                    context.SetValue(currentIndexKey, currentIndex);
                }
                
                Console.WriteLine($"[CompositeActivity] All activities completed during continuation");
                
                // Reset index for future runs
                context.SetValue(currentIndexKey, 0);

                // Copy isolated outputs back to parent context
                if (IsolateContext)
                    context.SetValue(Id, workingContext);
                
                // CRITICAL FIX: Signal completion to parent when all activities are done
                // This ensures the parent topic can continue to its next activity
                Console.WriteLine($"[CompositeActivity] Signaling completion to parent after continuation");
                TransitionTo(ActivityState.Completed, "All child activities completed during continuation");
                
                // Fire the completion events that the parent is waiting for
                var completedEvent = new ActivityCompletedEventArgs(Id, ActivityResult.Continue("Composite activity completed"));
                ActivityCompleted?.Invoke(this, completedEvent);
                
                Console.WriteLine($"[CompositeActivity] Completion signals sent to parent");
                    
            } catch (Exception ex) {
                Console.WriteLine($"[CompositeActivity] Error in ContinueExecutionAsync: {ex.Message}");
            }
        }
    }
}
