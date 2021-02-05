using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>Describes an individual transition.</summary>
    /// <remarks>
    /// Transitions:
    ///  - Each transition must have an event name, except the (optional) default transition identified by null.
    ///    If a transition for the event name is not found, the default transition is executed.
    ///  - Each transition may have a next state name otherwise stays in the same state.
    ///  - Each transition may have a transition action.
    /// </remarks>

    public class Transition
    {
        /// <summary>The name of the event that triggers this transition.</summary>
        public string EventName { get; internal set; } = null;

        /// <summary>Change state to this after execution action.</summary>
        public string NextState { get; internal set; } = null;

        /// <summary>Optional action - executed before state change</summary>
        public SmFunc TransitionFunc { get; internal set; } = null;

        /// <summary>Execute transition action.</summary>
        /// <param name="ei">Event information</param>
        /// <returns>The next state</returns>
        public string Execute(EventInfo ei)
        {
            TransitionFunc?.Invoke(ei.Param);
            return NextState;
        }
    }

    /// <summary>Specialized container. Has Add() to support initialization.</summary>
    public class Transitions : List<Transition>
    {
        public void Add(string evt, string nextState, SmFunc transFunc)
        {
            var trans = new Transition()
            {
                EventName = evt,
                NextState = nextState,
                TransitionFunc = transFunc
            };
            Add(trans);
        }
    }
}    