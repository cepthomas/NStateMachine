using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>
    /// Describes an individual state.
    ///  - Each state must have a name, except the(optional) default state identified by DEF_STATE.
    ///  - The current state is checked first, then the default state.
    ///  - Each state must have one or more Transitions.
    ///  - Each state may have an optional enter and/or exit action executed on state changes.Otherwise use NO_FUNC.
    /// </summary>
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

        /// <summary>Massaged runtime version of Transitions. Key is event name.</summary>
        public Dictionary<string, Transition> TransitionMap { get; init; } = new();
        #endregion

        #region Fields
        /// <summary>Convenience reference to optional default transition.</summary>
        private Transition _defaultTransition = null;
        #endregion

        #region Public functions
        /// <summary>Initialize the state and its transitions.</summary>
        /// <param name="stateNames">All valid state names</param>
        /// <returns>List of any errors.</returns>
        public List<string> Init(List<string> stateNames)
        {
            List<string> errors = new();

            // Basic sanity check.
            if (Transitions.Count == 0)
            {
                errors.Add($"No transitions for State[{StateName}]"); 
            }

            // Adjust transitions for DEF_STATE and SAME_STATE values.
            // Copy the transitions temporarily, ignoring the event names for now.
            Dictionary<string, Transition> tempTrans = new();
            Transitions.ForEach(t => { tempTrans.Add(tempTrans.Count.ToString(), t); });

            foreach (Transition t in tempTrans.Values)
            {
                // Handle default condition.
                if (t.EventName == SmEngine.DEF_EVENT)
                {
                    if (_defaultTransition is null)
                    {
                        _defaultTransition = t;
                    }
                    else
                    {
                        errors.Add($"Duplicate Default Event for State[{StateName}]");
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
                        errors.Add($"Duplicate EventName[{t.EventName}] for State[{StateName}]");
                    }
                }

                // Fix any SAME_STATE to current.
                if (t.NextState == SmEngine.SAME_STATE)
                {
                    t.NextState = StateName;
                }

                // Is the nextState valid?
                if (!stateNames.Contains(t.NextState))
                {
                    errors.Add($"Undefined NextState[{t.NextState}] for Event[{ t.EventName}] for State[{StateName}]");
                }
            }

            return errors;
        }

        /// <summary>Process the event. Execute transition if found, otherwise return null and let the caller handle it.</summary>
        /// <param name="ei">The event information.</param>
        /// <returns>The next state name.</returns>
        public string ProcessEvent(EventInfo ei)
        {
            // Get the transition associated with the event or default.
            var tx = TransitionMap.GetValueOrDefault(ei.Name, _defaultTransition);
            return tx?.Execute(ei);
        }

        /// <summary>Enter the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        public void Enter(object o) => EntryFunc?.Invoke(o);

        /// <summary>Exit the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        public void Exit(object o) => ExitFunc?.Invoke(o);

        /// <summary>Readable version.</summary>
        public override string ToString() => StateName;
        #endregion
    }

    /// <summary>Specialized container. Has Add() to support cleaner initialization.</summary>
    public class States : List<State>
    {
        public void Add(string stn, SmFunc entry, SmFunc exit, Transitions transitions) =>
           Add(new() { StateName = stn, EntryFunc = entry, ExitFunc = exit, Transitions = transitions });
    }
}    