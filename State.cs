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
    public class State<S, E> where S: Enum where E : Enum
    {
        #region Properties
        /// <summary>The state name.</summary>
        public S StateId { get; internal set; } = default;

        /// <summary>Optional state entry action.</summary>
        public SmFunc EntryFunc { get; init; } = null;

        /// <summary>Optional state exit action.</summary>
        public SmFunc ExitFunc { get; init; } = null;

        /// <summary>All the transitions possible for this state. Only used for initialization.</summary>
        public Transitions<S, E> Transitions { get; init; } = null;

        /// <summary>Massaged runtime version of Transitions. Key is event.</summary>
        public Dictionary<E, Transition<S, E>> TransitionMap { get; init; } = new();
        #endregion

        #region Fields
        /// <summary>Convenience reference to optional default transition.</summary>
        private Transition<S, E> _defaultTransition = null; //TODO keep or fold in with map?
        #endregion

        #region Public functions
        /// <summary>Initialize the state and its transitions.</summary>
//        /// <param name="states">All valid states</param>
        /// <returns>List of any errors.</returns>
        public List<string> Init()//List<S> states)
        {
            List<string> errors = new();

            // Basic sanity check.
            if (Transitions.Count == 0)
            {
                errors.Add($"No transitions for State[{StateId}]"); 
            }

            // Adjust transitions for DEF_STATE and SAME_STATE values.
            // Copy the transitions temporarily, ignoring the event names for now.
            Dictionary<string, Transition<S, E>> tempTrans = new();
            Transitions.ForEach(t => { tempTrans.Add(tempTrans.Count.ToString(), t); });

            foreach (Transition<S, E> t in tempTrans.Values)
            {
                // Handle default condition.
                if ((int)(object)t.EventId == 0)
                {
                    if (_defaultTransition is null)
                    {
                        _defaultTransition = t;
                    }
                    else
                    {
                        errors.Add($"Duplicate Default Event for State[{StateId}]");
                    }
                }
                else
                {
                    // Add to final map.
                    if (!TransitionMap.ContainsKey(t.EventId))
                    {
                        TransitionMap.Add(t.EventId, t);
                    }
                    else
                    {
                        errors.Add($"Duplicate EventName[{t.EventId}] for State[{StateId}]");
                    }
                }

                //// Is the nextState valid? TODO not needed
                //if (!states.Contains(t.NextState))
                //{
                //    errors.Add($"Undefined NextState[{t.NextState}] for Event[{ t.EventId}] for State[{StateId}]");
                //}
            }

            return errors;
        }

        /// <summary>Process the event. Execute transition if found, otherwise return indication and let the caller handle it.</summary>
        /// <param name="ei">The event information.</param>
        /// <returns>A tuple indicating if this was handled and if true the next state.</returns>
        public (bool handled, S state) ProcessEvent(EventInfo<S, E> ei)
        {
            bool handled = false;
            S state = default;

            // Get the transition associated with the event.
            if (TransitionMap.ContainsKey(ei.EventId))
            {
                state = TransitionMap[ei.EventId].Execute(ei);
                handled = true;
            }
            else if (_defaultTransition != null) // default handler?
            {
                state = _defaultTransition.Execute(ei);
                handled = true;
            }

            return (handled, state);
        }

        /// <summary>Enter the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        public void Enter(object o) => EntryFunc?.Invoke(o);

        /// <summary>Exit the state by executing the exit action</summary>
        /// <param name="o">Optional data object</param>
        public void Exit(object o) => ExitFunc?.Invoke(o);

        /// <summary>Readable version.</summary>
        public override string ToString() => $"{StateId}";
        #endregion
    }

    /// <summary>Specialized container. Has Add() to support cleaner initialization.</summary>
    public class States<S, E> : List<State<S, E>> where S : Enum where E : Enum
    {
        public void Add(S stn, SmFunc entry, SmFunc exit, Transitions<S, E> transitions) =>
           Add(new() { StateId = stn, EntryFunc = entry, ExitFunc = exit, Transitions = transitions });
    }
}    