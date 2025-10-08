using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConversaCore.StateMachine
{
    /// <summary>
    /// Represents a state transition
    /// </summary>
    /// <typeparam name="TState">The type of state</typeparam>
    public class StateTransition<TState> where TState : struct, Enum
    {
        /// <summary>
        /// Gets or sets the source state
        /// </summary>
        public TState From { get; set; }
        
        /// <summary>
        /// Gets or sets the target state
        /// </summary>
        public TState To { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp of the transition
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Gets or sets the reason for the transition
        /// </summary>
        public string Reason { get; set; }
    }
    
    /// <summary>
    /// State machine for managing topic states with validation and history
    /// </summary>
    /// <typeparam name="TState">The type of state</typeparam>
    public class TopicStateMachine<TState> : ITopicStateMachine<TState>, ITopicStateMachine where TState : struct, Enum
    {
        private TState _currentState;
        private readonly Dictionary<TState, HashSet<TState>> _allowedTransitions = new Dictionary<TState, HashSet<TState>>();
        private readonly Dictionary<(TState From, TState To), Func<bool>> _transitionGuards = new Dictionary<(TState, TState), Func<bool>>();
        private readonly List<StateTransition<TState>> _transitionHistory = new List<StateTransition<TState>>();
        private readonly Dictionary<TState, Func<TState, Task>> _entryActions = new Dictionary<TState, Func<TState, Task>>();
        private readonly Dictionary<TState, Func<TState, Task>> _exitActions = new Dictionary<TState, Func<TState, Task>>();
        
        /// <summary>
        /// The current state of the machine
        /// </summary>
        public TState CurrentState => _currentState;
        
        /// <summary>
        /// Gets the history of state transitions
        /// </summary>
        public IReadOnlyList<StateTransition<TState>> TransitionHistory => _transitionHistory.AsReadOnly();
        
        /// <summary>
        /// Creates a new state machine with the specified initial state
        /// </summary>
        /// <param name="initialState">The initial state</param>
        public TopicStateMachine(TState initialState)
        {
            _currentState = initialState;
            
            // Add initial entry to history
            _transitionHistory.Add(new StateTransition<TState>
            {
                From = default,
                To = initialState,
                Timestamp = DateTime.UtcNow,
                Reason = "Initial state"
            });
        }
        
        /// <summary>
        /// Configures an allowed transition between states
        /// </summary>
        /// <param name="from">The source state</param>
        /// <param name="to">The target state</param>
        /// <param name="guard">Optional guard condition that must be true for the transition to be allowed</param>
        public void ConfigureTransition(TState from, TState to, Func<bool> guard = null)
        {
            if (!_allowedTransitions.ContainsKey(from))
            {
                _allowedTransitions[from] = new HashSet<TState>();
            }
            
            _allowedTransitions[from].Add(to);
            
            if (guard != null)
            {
                _transitionGuards[(from, to)] = guard;
            }
        }
        
        /// <summary>
        /// Configures an action to execute when entering a state
        /// </summary>
        /// <param name="state">The state to configure the entry action for</param>
        /// <param name="action">The action to execute</param>
        public void ConfigureEntryAction(TState state, Func<TState, Task> action)
        {
            _entryActions[state] = action;
        }
        
        /// <summary>
        /// Configures an action to execute when exiting a state
        /// </summary>
        /// <param name="state">The state to configure the exit action for</param>
        /// <param name="action">The action to execute</param>
        public void ConfigureExitAction(TState state, Func<TState, Task> action)
        {
            _exitActions[state] = action;
        }
        
        /// <summary>
        /// Attempts to transition to the target state
        /// </summary>
        /// <param name="targetState">The state to transition to</param>
        /// <param name="reason">Optional reason for the transition (for logging)</param>
        /// <returns>True if the transition was successful, false otherwise</returns>
        public async Task<bool> TryTransitionAsync(TState targetState, string reason = null)
        {
            // Check if transition is allowed
            if (!IsTransitionAllowed(_currentState, targetState))
            {
                return false;
            }
            
            // Check guard condition if present
            if (!CheckTransitionGuard(_currentState, targetState))
            {
                return false;
            }
            
            // Execute exit action for current state if defined
            await ExecuteExitActionAsync(_currentState);
            
            // Perform transition
            var previousState = _currentState;
            _currentState = targetState;
            
            // Record transition in history
            _transitionHistory.Add(new StateTransition<TState>
            {
                From = previousState,
                To = targetState,
                Timestamp = DateTime.UtcNow,
                Reason = reason ?? "Explicit transition"
            });
            
            // Execute entry action for new state if defined
            await ExecuteEntryActionAsync(targetState, previousState);
            
            return true;
        }
        
        /// <summary>
        /// Forces a transition to the target state, bypassing guards and allowed transitions
        /// </summary>
        /// <param name="targetState">The state to transition to</param>
        /// <param name="reason">Reason for the forced transition</param>
        public async Task ForceTransitionAsync(TState targetState, string reason)
        {
            // Execute exit action for current state if defined
            await ExecuteExitActionAsync(_currentState);
            
            // Perform transition
            var previousState = _currentState;
            _currentState = targetState;
            
            // Record transition in history
            _transitionHistory.Add(new StateTransition<TState>
            {
                From = previousState,
                To = targetState,
                Timestamp = DateTime.UtcNow,
                Reason = $"FORCED: {reason}"
            });
            
            // Execute entry action for new state if defined
            await ExecuteEntryActionAsync(targetState, previousState);
        }
        
        /// <summary>
        /// Checks if a transition is allowed based on configured transitions
        /// </summary>
        /// <param name="from">Source state</param>
        /// <param name="to">Target state</param>
        /// <returns>True if the transition is allowed</returns>
        public bool IsTransitionAllowed(TState from, TState to)
        {
            return _allowedTransitions.ContainsKey(from) && _allowedTransitions[from].Contains(to);
        }
        
        /// <summary>
        /// Gets the allowed target states from the current state
        /// </summary>
        /// <returns>Collection of allowed target states</returns>
        public IEnumerable<TState> GetAllowedTransitions()
        {
            if (_allowedTransitions.ContainsKey(_currentState))
            {
                return _allowedTransitions[_currentState];
            }
            
            return Enumerable.Empty<TState>();
        }
        
        /// <summary>
        /// Gets the previous state
        /// </summary>
        /// <returns>The previous state, or null if there is no history</returns>
        public TState? GetPreviousState()
        {
            if (_transitionHistory.Count > 1)
            {
                return _transitionHistory[^2].To;
            }
            
            return null;
        }
        
        private bool CheckTransitionGuard(TState from, TState to)
        {
            if (_transitionGuards.TryGetValue((from, to), out var guard))
            {
                return guard();
            }
            
            return true; // No guard means always allowed
        }
        
        private async Task ExecuteEntryActionAsync(TState state, TState fromState)
        {
            if (_entryActions.TryGetValue(state, out var action))
            {
                await action(fromState);
            }
        }
        
        private async Task ExecuteExitActionAsync(TState state)
        {
            if (_exitActions.TryGetValue(state, out var action))
            {
                await action(_currentState);
            }
        }
    }
}