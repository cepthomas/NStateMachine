using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>Describes an individual transition. See README.md.</summary>
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

        /// <summary>Readable version.</summary>
        public override string ToString() => $"{EventName} -> {NextState}";
    }

    /// <summary>Specialized container. Has Add() to support cleaner initialization.</summary>
    public class Transitions : List<Transition>
    {
        public void Add(string evtName, string nextState, SmFunc transFunc) =>
            Add(new() { EventName = evtName, NextState = nextState, TransitionFunc = transFunc });
    }
}    