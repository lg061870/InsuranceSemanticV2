using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConversaCore.Events;
using ConversaCore.Cards;

namespace ConversaCore.TopicFlow.Activities
{
    /// <summary>
    /// A simple conditional activity that evaluates a condition and stores the result.
    /// Used by FlowBuilder for routing decisions.
    /// </summary>
    public class ConditionalActivity : TopicFlowActivity
    {
        private readonly Func<TopicWorkflowContext, string> _condition;

        /// <summary>
        /// Optional default decision label if the condition evaluation fails or returns null/empty.
        /// </summary>
        public string? DefaultDecision { get; set; }

        /// <summary>
        /// Branch map: condition value → target activityId
        /// </summary>
        private readonly Dictionary<string, string> _branches = new();

        public ConditionalActivity(string id, Func<TopicWorkflowContext, string> condition)
            : base(id)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        // === Factory Helpers ===

        public static ConditionalActivity Create(string id, Func<TopicWorkflowContext, string> condition)
            => new ConditionalActivity(id, condition);

        public static ConditionalActivity Create(
            string id,
            Func<TopicWorkflowContext, bool> condition,
            string trueLabel,
            string falseLabel)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            return new ConditionalActivity(id, ctx => condition(ctx) ? trueLabel : falseLabel);
        }

        public static ConditionalActivity CreateBranch<T>(
            string id,
            string contextKey,
            Dictionary<T, string> branches,
            string? defaultDecision = null) where T : notnull
        {
            if (string.IsNullOrEmpty(contextKey))
                throw new ArgumentNullException(nameof(contextKey));
            if (branches == null || branches.Count == 0)
                throw new ArgumentNullException(nameof(branches));

            var activity = new ConditionalActivity(id, ctx => {
                var value = ctx.GetValue<T>(contextKey);
                foreach (var branch in branches)
                {
                    if (Equals(value, branch.Key))
                        return branch.Value;
                }
                return defaultDecision ?? string.Empty;
            });

            activity.DefaultDecision = defaultDecision;
            return activity;
        }

        /// <summary>
        /// Add a branch mapping (used by FlowBuilder DSL).
        /// </summary>
        public void AddBranch(string conditionValue, string targetActivityId)
        {
            if (string.IsNullOrEmpty(conditionValue))
                throw new ArgumentNullException(nameof(conditionValue));
            if (string.IsNullOrEmpty(targetActivityId))
                throw new ArgumentNullException(nameof(targetActivityId));

            _branches[conditionValue] = targetActivityId;
        }

        /// <inheritdoc/>
        protected override Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var decision = _condition(context);

                if (string.IsNullOrEmpty(decision))
                {
                    if (!string.IsNullOrEmpty(DefaultDecision))
                        decision = DefaultDecision!;
                    else
                        throw new InvalidOperationException($"Condition in activity '{Id}' returned null or empty.");
                }

                // Record decision in context so later activities or FSM can inspect it
                context.SetValue($"{Id}_Decision", decision);

                // If explicit branch mapping exists, record it
                if (_branches.TryGetValue(decision, out var targetId))
                {
                    context.SetValue($"{Id}_Target", targetId);
                }

                // Surface decision as a message for logging/traceability
                return Task.FromResult(ActivityResult.Continue($"Condition evaluated → {decision}"));
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(DefaultDecision))
                {
                    context.SetValue($"{Id}_Decision", DefaultDecision);
                    return Task.FromResult(
                        ActivityResult.Continue($"⚠️ Condition error: {ex.Message}. Using default: {DefaultDecision}")
                    );
                }
                throw new InvalidOperationException($"Error evaluating condition in activity '{Id}'", ex);
            }
        }
    }

    /// <summary>
    /// A flow control activity that executes exactly one child activity based on runtime conditions.
    /// Leverages lessons learned from RepeatActivity for clean event forwarding and state management.
    /// </summary>
    /// <typeparam name="TActivity">The type of activity to execute conditionally</typeparam>
    public class ConditionalActivity<TActivity> : TopicFlowActivity, IAdaptiveCardActivity
        where TActivity : TopicFlowActivity
    {
        private readonly Dictionary<string, Func<string, TopicWorkflowContext, TActivity>> _branchFactories;
        private readonly Func<TopicWorkflowContext, (string branch, bool shouldExecute)> _conditionEvaluator;
        private readonly string? _defaultBranch;
        private readonly ILogger? _logger;
        
        // State management (lessons from RepeatActivity)
        private TActivity? _currentActivity;
        private TopicWorkflowContext? _activeContext;
        private string? _selectedBranch;
        private bool _conditionEvaluated = false;
        private bool _activityCompleted = false;

        // === IAdaptiveCardActivity Events - Forward from child activities ===
        public event EventHandler<CardJsonEventArgs>? CardJsonEmitted;
        public event EventHandler<CardJsonEventArgs>? CardJsonSending;
        public event EventHandler<CardJsonEventArgs>? CardJsonSent;
        public event EventHandler<CardJsonRenderedEventArgs>? CardJsonRendered;
        public event EventHandler<CardDataReceivedEventArgs>? CardDataReceived;
        public event EventHandler<ModelBoundEventArgs>? ModelBound;
        public event EventHandler<ValidationFailedEventArgs>? ValidationFailed;

        private ConditionalActivity(
            string id,
            Func<TopicWorkflowContext, (string branch, bool shouldExecute)> conditionEvaluator,
            Dictionary<string, Func<string, TopicWorkflowContext, TActivity>> branchFactories,
            string? defaultBranch = null,
            ILogger? logger = null)
            : base(id)
        {
            _conditionEvaluator = conditionEvaluator ?? throw new ArgumentNullException(nameof(conditionEvaluator));
            _branchFactories = branchFactories ?? throw new ArgumentNullException(nameof(branchFactories));
            _defaultBranch = defaultBranch;
            _logger = logger;
        }

        // === Factory Methods ===

        /// <summary>
        /// Simple binary condition (if/else)
        /// </summary>
        public static ConditionalActivity<TActivity> If(
            string id,
            Func<TopicWorkflowContext, bool> condition,
            Func<string, TopicWorkflowContext, TActivity> trueFactory,
            Func<string, TopicWorkflowContext, TActivity> falseFactory,
            ILogger? logger = null)
        {
            var branches = new Dictionary<string, Func<string, TopicWorkflowContext, TActivity>>
            {
                ["true"] = trueFactory,
                ["false"] = falseFactory
            };

            return new ConditionalActivity<TActivity>(
                id,
                ctx => {
                    var result = condition(ctx);
                    return (result ? "true" : "false", true);
                },
                branches,
                defaultBranch: "false",
                logger: logger
            );
        }

        /// <summary>
        /// Multi-branch condition (switch/case)
        /// </summary>
        public static ConditionalActivity<TActivity> Switch(
            string id,
            Func<TopicWorkflowContext, string> branchSelector,
            Dictionary<string, Func<string, TopicWorkflowContext, TActivity>> branches,
            string? defaultBranch = null,
            ILogger? logger = null)
        {
            return new ConditionalActivity<TActivity>(
                id,
                ctx => {
                    var branch = branchSelector(ctx);
                    var shouldExecute = !string.IsNullOrEmpty(branch) && (branches.ContainsKey(branch) || !string.IsNullOrEmpty(defaultBranch));
                    return (branch ?? defaultBranch ?? "", shouldExecute);
                },
                branches,
                defaultBranch: defaultBranch,
                logger: logger
            );
        }

        /// <summary>
        /// Context-aware condition with full control (most flexible)
        /// </summary>
        public static ConditionalActivity<TActivity> When(
            string id,
            Func<TopicWorkflowContext, (string branch, bool shouldExecute)> evaluator,
            Dictionary<string, Func<string, TopicWorkflowContext, TActivity>> branches,
            ILogger? logger = null)
        {
            return new ConditionalActivity<TActivity>(
                id,
                evaluator,
                branches,
                defaultBranch: null,
                logger: logger
            );
        }

        protected override async Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default)
        {
            _activeContext = context;
            
            // Evaluate condition once
            if (!_conditionEvaluated)
            {
                var (branch, shouldExecute) = _conditionEvaluator(context);
                _selectedBranch = branch;
                _conditionEvaluated = true;
                
                _logger?.LogWarning("[ConditionalActivity] Condition evaluated for {ActivityId} - Branch: '{Branch}', ShouldExecute: {ShouldExecute}", 
                    Id, branch, shouldExecute);
                
                // Store condition result in context
                context.SetValue($"{Id}_branch", branch);
                context.SetValue($"{Id}_shouldExecute", shouldExecute);
                
                if (!shouldExecute)
                {
                    _logger?.LogWarning("[ConditionalActivity] Condition evaluation resulted in no execution for {ActivityId}", Id);
                    return ActivityResult.Continue($"Condition evaluated → {branch} (skipped)");
                }
            }
            
            // Create and execute selected branch activity
            if (_currentActivity == null && _selectedBranch != null)
            {
                if (!_branchFactories.TryGetValue(_selectedBranch, out var factory))
                {
                    if (_defaultBranch != null && _branchFactories.TryGetValue(_defaultBranch, out factory))
                    {
                        _selectedBranch = _defaultBranch;
                        _logger?.LogWarning("[ConditionalActivity] Using default branch '{DefaultBranch}' for {ActivityId}", _defaultBranch, Id);
                    }
                    else
                    {
                        throw new InvalidOperationException($"No factory found for branch '{_selectedBranch}' and no default branch configured");
                    }
                }
                
                var childId = $"{Id}_{_selectedBranch}";
                _currentActivity = factory(childId, context);
                
                _logger?.LogWarning("[ConditionalActivity] Created child activity '{ChildId}' for branch '{Branch}'", childId, _selectedBranch);
                
                SubscribeToActivityEvents(_currentActivity);
            }
            
            if (_currentActivity == null)
            {
                throw new InvalidOperationException($"Failed to create child activity for ConditionalActivity '{Id}'");
            }
            
            // Execute the selected branch activity
            var result = await _currentActivity.RunAsync(context, input, cancellationToken);
            
            // Handle completion if activity is done
            if (!result.IsWaiting && !_activityCompleted)
            {
                _activityCompleted = true;
                context.SetValue($"{Id}_result", result.ModelContext);
                
                _logger?.LogWarning("[ConditionalActivity] Branch '{Branch}' completed for {ActivityId}", _selectedBranch, Id);
            }
            
            return result;
        }

        /// <summary>
        /// IAdaptiveCardActivity interface method - forward to child activity
        /// </summary>
        public void OnInputCollected(AdaptiveCardInputCollectedEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding OnInputCollected to child activity in branch '{Branch}'", _selectedBranch);
            
            if (_currentActivity is IAdaptiveCardActivity cardActivity)
            {
                cardActivity.OnInputCollected(e);
            }
        }

        /// <summary>
        /// Subscribe to child activity events for forwarding (RepeatActivity pattern)
        /// </summary>
        private void SubscribeToActivityEvents(TActivity activity)
        {
            if (activity is IAdaptiveCardActivity cardActivity)
            {
                cardActivity.CardJsonEmitted += OnChildCardJsonEmitted;
                cardActivity.CardJsonSending += OnChildCardJsonSending;
                cardActivity.CardJsonSent += OnChildCardJsonSent;
                cardActivity.CardJsonRendered += OnChildCardJsonRendered;
                cardActivity.CardDataReceived += OnChildCardDataReceived;
                cardActivity.ModelBound += OnChildModelBound;
                cardActivity.ValidationFailed += OnChildValidationFailed;
            }
        }

        /// <summary>
        /// Unsubscribe from child activity events
        /// </summary>
        private void UnsubscribeFromActivityEvents(TActivity activity)
        {
            if (activity is IAdaptiveCardActivity cardActivity)
            {
                cardActivity.CardJsonEmitted -= OnChildCardJsonEmitted;
                cardActivity.CardJsonSending -= OnChildCardJsonSending;
                cardActivity.CardJsonSent -= OnChildCardJsonSent;
                cardActivity.CardJsonRendered -= OnChildCardJsonRendered;
                cardActivity.CardDataReceived -= OnChildCardDataReceived;
                cardActivity.ModelBound -= OnChildModelBound;
                cardActivity.ValidationFailed -= OnChildValidationFailed;
            }
        }

        // === Event Forwarding Methods (RepeatActivity Pattern) ===
        
        private void OnChildCardJsonEmitted(object? sender, CardJsonEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding CardJsonEmitted from branch '{Branch}' - CardId: {CardId}", _selectedBranch, e.CardId);
            CardJsonEmitted?.Invoke(this, e);
        }

        private void OnChildCardJsonSending(object? sender, CardJsonEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding CardJsonSending from branch '{Branch}' - CardId: {CardId}", _selectedBranch, e.CardId);
            CardJsonSending?.Invoke(this, e);
        }

        private void OnChildCardJsonSent(object? sender, CardJsonEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding CardJsonSent from branch '{Branch}' - CardId: {CardId}", _selectedBranch, e.CardId);
            CardJsonSent?.Invoke(this, e);
        }

        private void OnChildCardJsonRendered(object? sender, CardJsonRenderedEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding CardJsonRendered from branch '{Branch}'", _selectedBranch);
            CardJsonRendered?.Invoke(this, e);
        }

        private void OnChildCardDataReceived(object? sender, CardDataReceivedEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding CardDataReceived from branch '{Branch}'", _selectedBranch);
            CardDataReceived?.Invoke(this, e);
        }

        private void OnChildModelBound(object? sender, ModelBoundEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding ModelBound from branch '{Branch}'", _selectedBranch);
            ModelBound?.Invoke(this, e);
        }

        private void OnChildValidationFailed(object? sender, ValidationFailedEventArgs e)
        {
            _logger?.LogWarning("[ConditionalActivity] Forwarding ValidationFailed from branch '{Branch}'", _selectedBranch);
            ValidationFailed?.Invoke(this, e);
        }
    }
}
