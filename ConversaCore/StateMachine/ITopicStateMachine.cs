using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConversaCore.StateMachine
{
    /// <summary>
    /// Interface for state machine that manages topic states
    /// </summary>
    /// <typeparam name="TState">The type of state</typeparam>
    public interface ITopicStateMachine<TState> where TState : struct, Enum
    {
        /// <summary>
        /// The current state of the machine
        /// </summary>
        TState CurrentState { get; }
        
        /// <summary>
        /// Gets the history of state transitions
        /// </summary>
        IReadOnlyList<StateTransition<TState>> TransitionHistory { get; }
        
        /// <summary>
        /// Configures an allowed transition between states
        /// </summary>
        /// <param name="from">The source state</param>
        /// <param name="to">The target state</param>
        /// <param name="guard">Optional guard condition that must be true for the transition to be allowed</param>
        void ConfigureTransition(TState from, TState to, Func<bool> guard = null);
        
        /// <summary>
        /// Configures an action to execute when entering a state
        /// </summary>
        /// <param name="state">The state to configure the entry action for</param>
        /// <param name="action">The action to execute</param>
        void ConfigureEntryAction(TState state, Func<TState, Task> action);
        
        /// <summary>
        /// Configures an action to execute when exiting a state
        /// </summary>
        /// <param name="state">The state to configure the exit action for</param>
        /// <param name="action">The action to execute</param>
        void ConfigureExitAction(TState state, Func<TState, Task> action);
        
        /// <summary>
        /// Attempts to transition to the target state
        /// </summary>
        /// <param name="targetState">The state to transition to</param>
        /// <param name="reason">Optional reason for the transition (for logging)</param>
        /// <returns>True if the transition was successful, false otherwise</returns>
        Task<bool> TryTransitionAsync(TState targetState, string reason = null);
        
        /// <summary>
        /// Forces a transition to the target state, bypassing guards and allowed transitions
        /// </summary>
        /// <param name="targetState">The state to transition to</param>
        /// <param name="reason">Reason for the forced transition</param>
        Task ForceTransitionAsync(TState targetState, string reason);
        
        /// <summary>
        /// Checks if a transition is allowed based on configured transitions
        /// </summary>
        /// <param name="from">Source state</param>
        /// <param name="to">Target state</param>
        /// <returns>True if the transition is allowed</returns>
        bool IsTransitionAllowed(TState from, TState to);
        
        /// <summary>
        /// Gets the allowed target states from the current state
        /// </summary>
        /// <returns>Collection of allowed target states</returns>
        IEnumerable<TState> GetAllowedTransitions();
        
        /// <summary>
        /// Gets the previous state
        /// </summary>
        /// <returns>The previous state, or null if there is no history</returns>
        TState? GetPreviousState();
    }
    
    /// <summary>
    /// Non-generic interface for state machine registration
    /// </summary>
    public interface ITopicStateMachine { }
}