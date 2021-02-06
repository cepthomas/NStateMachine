using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NStateMachine
{
    /// <summary>Definition for transition/entry/exit functions.</summary>
    /// <param name="o"></param>
    public delegate void SmFunc(object o);

    /// <summary>Logging.</summary>
    [Flags]
    public enum TraceLevel { None = 0, App = 1, Engine = 2 }

    /// <summary>Data carrying class. TODO1 use Record?</summary>
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
    /// Agnostic core engine of a state machine.
    /// </summary>
    public class SmEngine
    {
        #region Constants to make maps prettier
        public const SmFunc NO_FUNC = null;
        public const string DEF_STATE = "DEFAULT";
        public const string SAME_STATE = "";
        public const string DEF_EVENT = "DEFAULT";
        #endregion

        #region Fields
        /// <summary>All the states.</summary>
        Dictionary<string, State> _states = new();

        /// <summary>The default state if used.</summary>
        State _defaultState = null;

        /// <summary>The current state.</summary>
        State _currentState = null;

        /// <summary>The event queue.</summary>
        Queue<EventInfo> _eventQueue = new();

        /// <summary>Queue serializing access.</summary>
        object _locker = new();

        /// <summary>Flag to handle recursion in event processing.</summary>
        bool _processingEvents = false;
        #endregion

        #region Properties
        /// <summary>Readable version of current state.</summary>
        public string CurrentState => _currentState == null ? "" : _currentState.StateName;
       // public string CurrentState { get { return _currentState == null ? "" : _currentState.StateName; } }
//public readonly double Distance => Math.Sqrt(X * X + Y * Y);

        /// <summary>Accumulated list of errors.</summary>
        public List<string> Errors { get; } = new();

        /// <summary>For diagnostics.</summary>
        public TraceLevel TtraceLevel { get; set; } = TraceLevel.App;
        #endregion

        #region Public functions
        /// <summary>
        /// Generate DOT markup.
        /// </summary>
        /// <returns>Returns a string that contains the DOT markup.</returns>
        public string GenerateDot()
        {
            List<string> ls = new()
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

            // Generate actual nodes and edges from states. TODO1 options to add func names etc.
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
        #endregion

        #region Private and protected functions
        /// <summary>
        /// Init everything. Also does validation of the definitions at the same time.
        /// </summary>
        /// <param name="states">All the states.</param>
        /// <param name="initialState">Initial state.</param>
        /// <returns>Initialization success.</returns>
        protected bool InitSm(States states, string initialState)
        {
            Errors.Clear();
            _states.Clear();
            _eventQueue.Clear();

            try// TODO1 patterns?
            {
                ////// Populate our collection from the client.
                foreach (State st in states)
                {
                    if (st.Transitions is null || st.Transitions.Count == 0)
                    {
                        Errors.Add($"No transitions for State:[{st.StateName}]");
                    }
                    else
                    {
                        // Check for default state.
                        if(st.StateName == DEF_STATE)
                        {
                            if(_defaultState == null)
                            {
                                st.StateName = DEF_STATE;
                                _defaultState = st;
                            }
                            else
                            {
                                Errors.Add($"Multiple default states");
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
                                Errors.Add($"Duplicate State Name:[{st.StateName}]");
                            }
                        }
                    }
                }

                if(_defaultState != null)
                {
                    _states.Add("", _defaultState);
                }

                //////// Sanity checking on the transitions.
                List<string> keyList = new(_states.Keys);

                foreach (State st in _states.Values)// also _default...
                {
                    Errors.AddRange(st.Init(keyList));
                }

                if (!string.IsNullOrEmpty(initialState) && _states.ContainsKey(initialState))
                {
                    _currentState = _states[initialState];
                    _currentState.Enter(null);
                }
                else // invalid initial state
                {
                    Errors.Add($"Invalid Initial State:[{initialState}]");
                }

                if (!string.IsNullOrEmpty(initialState) && _states.ContainsKey(initialState))
                {
                    _currentState = _states[initialState];
                    _currentState.Enter(null);
                }
                else // invalid initial state
                {
                    Errors.Add($"Invalid Initial State:[{initialState}]");
                }
            }
            catch (Exception e)
            {
                Errors.Add($"Exception during initializing:{e.Message} ({e.StackTrace})");
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
        protected bool ProcessEvent(string evt, object o = null)
        {
            bool ok = true;

            lock (_locker)
            {
                // Add the event to the queue.
                _eventQueue.Enqueue(new() { Name = evt, Param = o });

                // Check for recursion through the processing loop - event may be generated internally during processing.
                if (!_processingEvents)
                {
                    _processingEvents = true;

                    // Process all events in the event queue. // TODO1 patterns?
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
                                nextStateName = _currentState.ProcessEvent(ei);
                            }

                            if (nextStateName is null)
                            {
                                throw new Exception($"State:[{_currentState.StateName}] Invalid event:[{ei.Name}]");
                            }

                            // Is there a state change?
                            if (nextStateName != _currentState.StateName)
                            {
                                // Get the next state.
                                State nextState = _states[nextStateName];

                                // Exit current state.
                                _currentState.Exit(ei.Param);

                                // Set new state.
                                _currentState = nextState;

                                // Enter new state.
                                _currentState.Enter(ei.Param);
                            }
                        }
                        catch (Exception e) //TODO1 better run time handling - ask client?
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

        /// <summary>Adjust to taste.</summary>
        /// <param name="s"></param>
        /// <param name="s">lvl</param>
        protected void Trace(TraceLevel lvl, string s)
        {
            if ((TtraceLevel & lvl) > 0)
            {
                Debug.WriteLine($"{DateTime.Now.ToString("yyyy'-'MM'-'dd HH':'mm':'ss.fff")} {lvl} {s}");
            }
        }
        #endregion
    }
}    