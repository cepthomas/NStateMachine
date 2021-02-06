using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>Describes an individual state.</summary>
    /// <remarks>
    /// States:
    /// - Each state must have a name, except the (optional) default state identified by null.
    ///   The default state is checked first, then the current state.
    /// - Each state must have one or more Transitions.
    /// - Each state may have an enter and/or exit action executed on state changes.
    /// </remarks>
    public class State
    {
        #region Properties
        /// <summary>The state name.</summary>
        public string StateName { get; internal set; } = "???";

        /// <summary>All the transitions possible for this state.</summary>
        public Dictionary<string, Transition> Transitions { get; init; } = new();
        #endregion

        #region Private fields
        /// <summary>Convenience reference to optional default transition.</summary>
        private Transition _defaultTransition = null;

        /// <summary>Optional state entry action.</summary>
        public SmFunc _entryFunc { get; init; } = null;

        /// <summary>Optional state exit action.</summary>
        public SmFunc _exitFunc { get; init; } = null;
        #endregion

        #region Public methods
        /// <summary>Initialize the state and its transitions.</summary>
        /// <param name="stateNames">All valid state names</param>
        /// <returns>List of any errors.</returns>
        public List<string> Init(List<string> stateNames)
        {
            List<string> errors = new();

            // Adjust transitions for DEFAULT_EVENT and SAME_STATE conditions.
            // First take a copy of the current.
            Dictionary<string, Transition> tempTrans = Transitions;

            Transitions.Clear();

            foreach (Transition t in tempTrans.Values)
            {
                if (string.IsNullOrEmpty(t.EventName))
                {
                    if (_defaultTransition is null)
                    {
                        _defaultTransition = t;
                    }
                    else
                    {
                        string serr = $"Duplicate Default Event defined for:{StateName}";
                        errors.Add(serr);
                    }
                }
                else
                {
                    if (!Transitions.ContainsKey(t.EventName))
                    {
                        Transitions.Add(t.EventName, t);
                    }
                    else
                    {
                        string serr = $"Duplicate Event Name:{t.EventName}";
                        errors.Add(serr);
                    }
                }

                // Fix any SAME_STATE to current.
                string nextState = t.NextState;
                if (string.IsNullOrEmpty(nextState))
                {
                    t.NextState = StateName;
                }

                // Is the nextState valid?
                if (!stateNames.Contains(t.NextState))
                {
                    string serr = $"Undefined NextState:{ t.NextState}";
                    errors.Add(serr);
                }
            }

            return errors;
        }

        /// <summary>Process the event.</summary>
        /// <param name="ei">The event information.</param>
        /// <returns>The next state name.</returns>
        public string ProcessEvent(EventInfo ei)
        {
            string nextState = null;

            if (Transitions != null)
            {
                // Get the transition associated with the event.
                if (!Transitions.TryGetValue(ei.Name, out Transition tx))
                {
                    tx = _defaultTransition;
                }

                // Execute transition if found, otherwise return the null and let the caller handle it.
                if (tx != null)
                {
                    nextState = tx.Execute(ei);
                }
            }

            return nextState;
        }

        /// <summary>Enter the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        /// <returns>void</returns>
        public void Enter(object o)
        {
            _entryFunc?.Invoke(o);
        }

        /// <summary>Exit the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        /// <returns>void</returns>
        public void Exit(object o)
        {
            _exitFunc?.Invoke(o);
        }
        #endregion
    }

    /// <summary>Specialized container. Has Add() to support initialization.</summary>
    public class States : List<State>
    {
        public void Add(string stn, SmFunc entry, SmFunc exit, Transitions transitions)
        {
            State state = new()
            {
                StateName = stn,
                _entryFunc = entry,
                _exitFunc = exit,
                Transitions = new()
            };

            // Copy the transitions temporarily, ignoring the event names for now.
            for (int i = 0; i < transitions.Count; i++)
            {
                state.Transitions.Add(i.ToString(), transitions[i]);
            }

            Add(state);
        }
    }
}    