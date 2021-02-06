using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>Describes an individual state. See README.md.</summary>
    public class State
    {
        #region Properties
        /// <summary>The state name.</summary>
        public string StateName { get; internal set; } = "???";

        /// <summary>Optional state entry action.</summary>
        public SmFunc EntryFunc { get; init; } = null;

        /// <summary>Optional state exit action.</summary>
        public SmFunc ExitFunc { get; init; } = null;

        /// <summary>All the transitions possible for this state. Only used for initialization.</summary>
        public Transitions Transitions { get; init; } = null;

        /// <summary>Massaged runtime version of Transitions.</summary>
        public Dictionary<string, Transition> TransitionMap { get; init; } = new();
        #endregion

        #region Fields
        /// <summary>Convenience reference to optional default transition.</summary>
        private Transition _defaultTransition = null;
        #endregion

        #region Public methods
        /// <summary>Initialize the state and its transitions.</summary>
        /// <param name="stateNames">All valid state names</param>
        /// <returns>List of any errors.</returns>
        public List<string> Init(List<string> stateNames)
        {
            List<string> errors = new();

            // Basic sanity check.
            if (Transitions.Count == 0)
            {
                errors.Add($"No transitions for State:[{StateName}]");
            }

            ///// Adjust transitions for DEFAULT_EVENT and SAME_STATE conditions.

            // Copy the transitions temporarily, ignoring the event names for now.
            Dictionary<string, Transition> tempTrans = new();
            Transitions.ForEach(t => { tempTrans.Add(tempTrans.Count.ToString(), t); });

            foreach (Transition t in tempTrans.Values)
            {
                // Handle default condition. TODO1 patterns
                if (t.EventName == SmEngine.DEF_STATE)
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
                    // Add to final map.
                    if (!TransitionMap.ContainsKey(t.EventName))
                    {
                        TransitionMap.Add(t.EventName, t);
                    }
                    else
                    {
                        string serr = $"Duplicate Event Name:{t.EventName}";
                        errors.Add(serr);
                    }
                }

                // Fix any SAME_STATE to current.
                string nextState = t.NextState;
                if(nextState == SmEngine.SAME_STATE)
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

            if (TransitionMap != null)
            {
                // Get the transition associated with the event.
                if (!TransitionMap.TryGetValue(ei.Name, out Transition tx))
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
            EntryFunc?.Invoke(o);
        }

        /// <summary>Exit the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        /// <returns>void</returns>
        public void Exit(object o)
        {
            ExitFunc?.Invoke(o);
        }
        #endregion
    }

    /// <summary>Specialized container. Has Add() to support initialization.</summary>
    public class States : List<State>
    {
        public void Add(string stn, SmFunc entry, SmFunc exit, Transitions transitions)
        {
           Add(new()
            {
                StateName = stn,
                EntryFunc = entry,
                ExitFunc = exit,
                Transitions = transitions
            });
        }
    }
}    