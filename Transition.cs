using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>Describes an individual transition.
    ///   - Each transition must have an event name, except the (optional) default transition identified by DEF_EVENT.
    ///   - If a transition for the event name is not found, the DEF_EVENT transition is executed.
    ///   - Each transition may have a next state name or SAME_STATE which stays in the same state.
    ///   - Each transition may have an optional transition action. Otherwise use NO_FUNC.
    /// </summary>
    public class Transition<S, E> where S : Enum where E : Enum
    {
        /// <summary>The name of the event that triggers this transition.</summary>
        public E EventId { get; internal set; } = default;

        /// <summary>Change state to this after execution action.</summary>
        public S NextState { get; internal set; } = default;

        /// <summary>Optional action - executed before state change</summary>
        public SmFunc TransitionFunc { get; internal set; } = null;

        /// <summary>Execute transition action.</summary>
        /// <param name="ei">Event information</param>
        /// <returns>The next state</returns>
        public S Execute(EventInfo<S, E> ei)
        {
            TransitionFunc?.Invoke(ei.Param);
            return NextState;
        }

        /// <summary>Readable version.</summary>
        public override string ToString() => $"{EventId} -> {NextState}";
    }

    /// <summary>Specialized container. Has Add() to support cleaner initialization.</summary>
    public class Transitions<S, E> : List<Transition<S, E>> where S : Enum where E : Enum
    {
        public void Add(E evt, S nextState, SmFunc transFunc) =>
            Add(new() { EventId = evt, NextState = nextState, TransitionFunc = transFunc });
    }
}    