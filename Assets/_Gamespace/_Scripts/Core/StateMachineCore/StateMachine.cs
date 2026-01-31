using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace _Workspace._Scripts.Core.StateMachineCore
{
    /// <summary>
    /// Lightweight, generic finite state machine intended for Unity workflows.
    /// Manages registration, initialization, ticking, and transitions between states of type <typeparamref name="TState"/>.
    /// </summary>
    public sealed class StateMachine<TState> where TState : Enum
    {
        private readonly Dictionary<TState, IState> _states;
        private TState _currentState;
        private bool _isInitialized;

        private bool _inTransition;
        private bool _hasPendingTransition;
        private TState _pendingNext;

        public bool ThrowOnError { get; set; } = false;
        public bool ErrorOnSameState { get; set; } = false;
        public bool ErrorIfUninitializedTick { get; set; } = true;

        /// <summary>
        /// Raised whenever an error is detected. The first parameter is a formatted diagnostic message; the second is the captured exception (if any).
        /// </summary>
        public event Action<string, Exception> ErrorRaised;

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Emits a formatted error through <see cref="ErrorRaised"/> and either throws or logs it based on <see cref="ThrowOnError"/>.
        /// Automatically captures call-site information for easier debugging.
        /// </summary>
        /// <param name="message">Human-readable description of the error.</param>
        /// <param name="ex">Optional exception associated with the error.</param>
        /// <param name="caller">Member name of the call-site (autofilled).</param>
        /// <param name="file">Source file path of the call-site (auto-filled).</param>
        /// <param name="line">Line number of the call-site (autofilled).</param>
        private void RaiseError(
            string message,
            Exception ex = null,
            [CallerMemberName] string caller = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            var formatted =
                $"[StateMachine<{typeof(TState).Name}>] {message}\n" +
                $"Current={_currentState}, Previous={PreviousState}\n" +
                $"Caller={caller} ({System.IO.Path.GetFileName(file)}:{line})";

            ErrorRaised?.Invoke(formatted, ex);

            if (ThrowOnError) throw ex ?? new InvalidOperationException(formatted);
            Debug.LogError(formatted);
        }

        /// <summary>
        /// The state currently active in the state machine.
        /// </summary>
        public TState CurrentState => _currentState;

        /// <summary>
        /// The state that was active immediately before the current one.
        /// </summary>
        public TState PreviousState { get; private set; }

        /// <summary>
        /// Fired after a successful state transition. Parameters are <c>(from, to)</c>.
        /// </summary>
        public event Action<TState, TState> StateChanged;

        /// <summary>
        /// Creates a new state machine.
        /// </summary>
        /// <param name="states">Dictionary mapping states to their handlers.</param>
        /// <param name="initialState">The state to set as current before initialization.</param>
        public StateMachine(Dictionary<TState, IState> states, TState initialState)
        {
            this._states = states ?? throw new ArgumentNullException(nameof(states));
            _currentState = initialState;
            PreviousState = initialState;
            if (!this._states.ContainsKey(initialState))
            {
                RaiseError($"Initial state '{initialState}' is not registered in the state dictionary.");
            }
        }

        /// <summary>
        /// Marks the machine as initialized and optionally calls <c>Enter()</c> on the initial state.
        /// </summary>
        /// <param name="callEnter">If <c>true</c>, invokes <c>Enter()</c> on the current (initial) state.</param>
        public void Initialize(bool callEnter = true)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            if (!callEnter) return;

            if (_states.TryGetValue(_currentState, out var current))
            {
                SafeCall(current.Enter, $"Enter() of state '{_currentState}'");
            }
            else
            {
                RaiseError($"Initial state '{_currentState}' not found in the state dictionary.");
            }
        }

        /// <summary>
        /// Attempts to transition to <paramref name="nextState"/> without throwing on failure.
        /// Returns <c>true</c> if the transition succeeds; otherwise <c>false</c>.
        /// </summary>
        /// <param name="nextState">The target state.</param>
        /// <returns><c>true</c> if the state changed; otherwise <c>false</c>.</returns>
        public bool TryChangeState(TState nextState)
        {
            if (_currentState.Equals(nextState))
            {
                if (ErrorOnSameState)
                    RaiseError($"Attempted to change to the same state '{nextState}'.");
                return false;
            }

            if (!_states.TryGetValue(nextState, out var next))
            {
                RaiseError($"Next state '{nextState}' not found in the state dictionary (TryChangeState).");
                return false;
            }

            if (_inTransition)
            {
                _hasPendingTransition = true;
                _pendingNext = nextState;
                return true;
            }

            _inTransition = true;

            if (_states.TryGetValue(_currentState, out var current))
            {
                SafeCall(current.Exit, $"Exit() of state '{_currentState}'");
            }

            var from = _currentState;
            PreviousState = from;
            _currentState = nextState;

            SafeCall(next.Enter, $"Enter() of state '{nextState}'");

            StateChanged?.Invoke(from, nextState);

            _inTransition = false;

            if (_hasPendingTransition)
            {
                var queued = _pendingNext;
                _hasPendingTransition = false;
                _pendingNext = default;

                ChangeState(queued);
            }

            return true;
        }

        /// <summary>
        /// Transitions to <paramref name="nextState"/> and reports detailed errors if the transition is invalid.
        /// </summary>
        /// <param name="nextState">The target state.</param>
        public void ChangeState(TState nextState)
        {
            TryChangeState(nextState);
        }

        /// <summary>
        /// Forwards a per-frame update to the current state. Intended to be called from <c>Update()</c>.
        /// </summary>
        public void Tick()
        {
            if (!_isInitialized)
            {
                if (ErrorIfUninitializedTick)
                    RaiseError("Tick() called before Initialize().");
                return;
            }

            if (_states.TryGetValue(_currentState, out var current))
            {
                SafeCall(current.Tick, $"Tick() of state '{_currentState}'");
            }
            else
            {
                RaiseError($"Current state '{_currentState}' not found in the state dictionary during Tick().");
            }
        }

        /// <summary>
        /// Forwards a fixed-step update to the current state. Intended to be called from <c>FixedUpdate()</c>.
        /// </summary>
        public void FixedTick()
        {
            if (!_isInitialized)
            {
                if (ErrorIfUninitializedTick)
                    RaiseError("FixedTick() called before Initialize().");
                return;
            }

            if (_states.TryGetValue(_currentState, out var current))
            {
                SafeCall(current.FixedTick, $"FixedTick() of state '{_currentState}'");
            }
            else
            {
                RaiseError($"Current state '{_currentState}' not found in the state dictionary during FixedTick().");
            }
        }

        
        // ReSharper disable Unity.PerformanceAnalysis
        private void SafeCall(Action action, string context)
        {
            try { action?.Invoke(); }
            catch (Exception e) { RaiseError($"Exception during {context} of state '{_currentState}'.", e); }
        }
    }
}