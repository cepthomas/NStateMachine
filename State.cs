using System;
using System.Collections.Generic;


namespace Ephemera.NStateMachine
{
    /// <summary>Describes an individual state. See README.md for usage.</summary>
    public class State<S, E> where S : Enum where E : Enum
    {
        #region Fields
        /// <summary>Massaged runtime version of Transitions. Key is event.</summary>
        readonly Dictionary<E, Transition<S, E>> _transitionMap = new();
        #endregion

        #region Properties
        /// <summary>The state id.</summary>
        public S StateId { get; internal set; } = Common<S, E>.DEFAULT_STATE_ID;

        /// <summary>Optional state entry action.</summary>
        public SmFunc? EntryFunc { get; init; } = null;

        /// <summary>Optional state exit action.</summary>
        public SmFunc? ExitFunc { get; init; } = null;

        /// <summary>All the transitions possible for this state. Only used for initialization.</summary>
        public Transitions<S, E> Transitions { get; init; } = new();
        #endregion

        #region Public functions
        /// <summary>Initialize the state and its transitions.</summary>
        /// <returns>List of any errors.</returns>
        public List<string> Init()
        {
            List<string> errors = new();

            // Basic sanity check.
            if (Transitions.Count == 0)
            {
                errors.Add($"No transitions for State[{StateId}]"); 
            }

            // Copy the transitions temporarily, ignoring the event names for now.
            Dictionary<string, Transition<S, E>> tempTrans = new();
            Transitions.ForEach(t => { tempTrans.Add(tempTrans.Count.ToString(), t); });

            foreach (Transition<S, E> t in tempTrans.Values)
            {
                // Add to final map.
                if (!_transitionMap.ContainsKey(t.EventId))
                {
                    _transitionMap.Add(t.EventId, t);
                }
                else
                {
                    errors.Add($"Duplicate EventName[{t.EventId}] for State[{StateId}]");
                }
            }

            return errors;
        }

        /// <summary>Process the event. Execute transition if found, otherwise return indication and let the caller handle it.</summary>
        /// <param name="ei">The event information.</param>
        /// <returns>A tuple indicating if this was handled and if true the next state.</returns>
        public (bool handled, S? state) ProcessEvent(EventInfo<S, E> ei)
        {
            bool handled = false;
            S? state = default;

            // Get the transition associated with the event.
            if (_transitionMap.ContainsKey(ei.EventId))
            {
                state = _transitionMap[ei.EventId].Execute(ei);
                handled = true;
            }
            else if (_transitionMap.ContainsKey(Common<S, E>.DEFAULT_EVENT_ID))
            {
                state = _transitionMap[Common<S, E>.DEFAULT_EVENT_ID].Execute(ei);
                handled = true;
            }

            return (handled, state);
        }

        /// <summary>Enter the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        public void Enter(object? o = null) => EntryFunc?.Invoke(o);

        /// <summary>Exit the state by executing the exit action</summary>
        /// <param name="o">Optional data object</param>
        public void Exit(object? o = null) => ExitFunc?.Invoke(o);

        /// <summary>Readable version.</summary>
        public override string ToString() => $"{StateId}";
        #endregion
    }

    /// <summary>Specialized container for syntactic sugar.</summary>
    public class States<S, E> : List<State<S, E>> where S : Enum where E : Enum
    {
        /// <summary>Has Add() to support cleaner initialization.</summary>
        /// <param name="stn"></param>
        /// <param name="entry"></param>
        /// <param name="exit"></param>
        /// <param name="transitions"></param>
        public void Add(S stn, SmFunc? entry, SmFunc? exit, Transitions<S, E> transitions) =>
           Add(new() { StateId = stn, EntryFunc = entry, ExitFunc = exit, Transitions = transitions });
    }
}    