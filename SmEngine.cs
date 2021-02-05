using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>
    /// Definition for transition/entry/exit functions.
    /// </summary>
    /// <param name="o"></param>
    public delegate void SmFunc(object o);

    /// <summary>Data carrying class. TODO Record?</summary>
    public class EventInfo
    {
        /// <summary>Unique event name.</summary>
        public string Name { get; set; } = "???";

        /// <summary>Event data.</summary>
        public object Param { get; set; } = null;

        /// <summary>Generate a human readable string.</summary>
        public override string ToString()
        {
            return $"Event:{Name} Param:{Param ?? "null"}";
        }
    }

    /// <summary>
    /// A generalized implementation of a state machine.
    /// </summary>
    /// <remarks>
    /// States:
    /// - Each state must have a name, except the (optional) default state identified by null.
    ///   The default state is checked first, then the current state.
    /// - Each state must have one or more Transitions.
    /// - Each state may have an enter and/or exit action executed on state changes.
    /// 
    /// Transitions:
    ///  - Each transition must have an event name, except the (optional) default transition identified by null.
    ///    If a transition for the event name is not found, the default transition is executed.
    ///  - Each transition may have a next state name otherwise stays in the same state.
    ///  - Each transition may have a transition action.
    /// </remarks>
    public class SmEngine
    {
        #region Fields
        /// <summary>All the states.</summary>
        Dictionary<string, State> _states = new Dictionary<string, State>();

        /// <summary>The default state if used.</summary>
        State _defaultState = null;

        /// <summary>The event queue.</summary>
        Queue<EventInfo> _eventQueue = new Queue<EventInfo>();

        /// <summary>Queue serializing access.</summary>
        object _locker = new object();

        /// <summary>Flag to handle recursion in event processing.</summary>
        bool _processingEvents = false;
        #endregion

        #region Properties
        /// <summary>The machine current state.</summary>
        public State CurrentState { get; private set; } = null;

        /// <summary>Accumulated list of errors.</summary>
        public List<string> Errors { get; } = new List<string>();
        #endregion

        /// <summary>
        /// Init everything. Also does validation of the definitions at the same time.
        /// </summary>
        /// <param name="states">All the states.</param>
        /// <param name="initialState">Initial state.</param>
        /// <returns>Initialization success.</returns>
        public bool Init(States states, string initialState)
        {
            Errors.Clear();
            _states.Clear();
            _eventQueue.Clear();

            try
            {
                ////// Populate our collection from the client.
                foreach (State st in states)
                {
                    if (st.Transitions is null || st.Transitions.Count == 0)
                    {
                        string serr = $"No transitions for State:{st.StateName}";
                        Errors.Add(serr);
                    }
                    else
                    {
                        // Check for default state.
                        if(st.StateName == null)
                        {
                            if(_defaultState == null)
                            {
                                st.StateName = "Default";
                                _defaultState = st;
                            }
                            else
                            {
                                string serr = $"Multiple default states";
                                Errors.Add(serr);
                            }
                        }
                        else
                        {
                            // Check for duplicate state names.
                            if (!_states.ContainsKey(st.StateName))
                            {
                                _states.Add(st.StateName, st);
                            }
                            else
                            {
                                string serr = $"Duplicate State Name:{st.StateName}";
                                Errors.Add(serr);
                            }
                        }
                    }
                }

                if(_defaultState != null)
                {
                    _states.Add("", _defaultState);
                }

                //////// Sanity checking on the transitions.
                List<string> keyList = new List<string>(_states.Keys);

                foreach (State st in _states.Values)// also _default...
                {
                    Errors.AddRange(st.Init(keyList));
                }

                if (!string.IsNullOrEmpty(initialState) && _states.ContainsKey(initialState))
                {
                    CurrentState = _states[initialState];
                    CurrentState.Enter(null);
                }
                else // invalid initial state
                {
                    string serr = $"Invalid Initial State:{initialState}";
                    Errors.Add(serr);
                }

                if (!string.IsNullOrEmpty(initialState) && _states.ContainsKey(initialState))
                {
                    CurrentState = _states[initialState];
                    CurrentState.Enter(null);
                }
                else // invalid initial state
                {
                    string serr = $"Invalid Initial State:{initialState}";
                    Errors.Add(serr);
                }
            }
            catch (Exception e)
            {
                string serr = $"Exception during initializing:{e.Message} ({e.StackTrace})";
                Errors.Add(serr);
            }

            return Errors.Count == 0;
        }

        /// <summary>
        /// Processes an event. Returns when event queue is empty.
        /// Events can be coming on different threads so this method is locked.
        /// </summary>
        /// <param name="evt">Incoming event.</param>
        /// <param name="o">Optional event data.</param>
        /// <returns>Ok or error.</returns>
        public bool ProcessEvent(string evt, object o = null)
        {
            bool ok = true;

            lock (_locker)
            {
                // Add the event to the queue.
                _eventQueue.Enqueue(new EventInfo() { Name = evt, Param = o });

                // Check for recursion through the processing loop - event may be generated internally during processing.
                if (!_processingEvents)
                {
                    _processingEvents = true;

                    // Process all events in the event queue.
                    while (_eventQueue.Count > 0 && ok)
                    {
                        EventInfo ei = _eventQueue.Dequeue();
                        try
                        {
                            // Dig out the correct transition if there is one.
                            string nextStateName = null;

                            // Try default state first.
                            if(_defaultState != null)
                            {
                                nextStateName = _defaultState.ProcessEvent(ei);
                            }

                            if (nextStateName is null)
                            {
                                // Try current state.
                                nextStateName = CurrentState.ProcessEvent(ei);
                            }

                            if (nextStateName is null)
                            {
                                throw new Exception($"State: {CurrentState.StateName} Invalid event: {ei.Name}");
                            }

                            // Is there a state change?
                            if (nextStateName != CurrentState.StateName)
                            {
                                // Get the next state.
                                State nextState = _states[nextStateName];

                                // Exit current state.
                                CurrentState.Exit(ei.Param);

                                // Set new state.
                                CurrentState = nextState;

                                // Enter new state.
                                CurrentState.Enter(ei.Param);
                            }
                        }
                        catch (Exception e)
                        {
                            // Add to the list of errors.
                            Errors.Add(e.Message);

                            // Set the return status.
                            ok = false;

                            // Clean up.
                            _eventQueue.Clear();
                            _processingEvents = false;

                            // Rethrow.
                            //throw;
                        }
                    }
                }

                // Done for now.
                _processingEvents = false;

                return ok;
            }
        }

        /// <summary>
        /// Generate DOT markup.
        /// </summary>
        /// <returns>Returns a string that contains the DOT markup.</returns>
        public string GenerateDot()
        {
            List<string> ls = new List<string>
            {
                "digraph StateDiagram {",
                // Init attributes for dot.
                "    ratio=\"compress\";",
                "    fontname=\"Arial\";",
                "    label=\"\";", // (your label here!)
                "    node [",
                "    height=\"0.50\";",
                "    width=\"1.0\";",
                "    shape=\"ellipse\";",
                "    fixedsize=\"true\";",
                "    fontsize=\"8\";",
                "    fontname=\"Arial\";",
                "];",
                "",
                "    edge [",
                "    fontsize=\"8\";",
                "    fontname=\"Arial\";",
                "];",
                ""
            };

            // Generate actual nodes and edges from states. TODO options to add func names etc.
            foreach (State s in _states.Values)
            {
                // Write a node for the state.
                //ls.Add($"    \"{s.StateName}\";");

                // Iterate through the state transitions.
                foreach (KeyValuePair<string, Transition> kvp in s.Transitions)
                {
                    Transition t = kvp.Value;

                    // Get event name, but strip off "Transition" suffix if present to save space.
                    //string transitionSuffix = "Transition";
                    string eventName = t.EventName;
                    //if (eventName.EndsWith(transitionSuffix))
                    //{
                    //    eventName = eventName.Substring(0, eventName.Length - transitionSuffix.Length);
                    //}

                    // Write an edge for the transition
                    string nextState = t.NextState;
                    //if (nextState == "SAME_STATE")
                    //{
                    //    nextState = s.StateName;
                    //}
                    ls.Add($"        \"{s.StateName}\" -> \"{nextState}\" [label=\"{eventName}\"];");
                }

                //ls.Add("{0}");
            }

            ls.Add("}");

            return string.Join(Environment.NewLine, ls);
        }
    }
}    