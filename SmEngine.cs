using System;
using System.Collections.Generic;
using System.Diagnostics;


// TODO  You can use the ??= operator to assign the value of its right-hand operand to its left-hand operand only if
// the left-hand operand evaluates to null.
// ?? returns the value of its left-hand operand if it isn't null; otherwise, it evaluates the right-hand operand and returns its result.
// FormsAuth = formsAuth ?? new FormsAuthenticationWrapper();

namespace NStateMachine
{
    /// <summary>Definition for transition/entry/exit functions.</summary>
    /// <param name="o"></param>
    public delegate void SmFunc(object o);

    /// <summary>Logging.</summary>
    [Flags]
    public enum TraceLevel { None = 0, App = 1, Eng = 2 }

    /// <summary>Data carrying class.</summary>
    public record EventInfo(string Name, object Param);

    /// <summary>Agnostic core engine of a state machine.</summary>
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
        Dictionary<string, State> _stateMap = new();

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

        /// <summary>Accumulated list of errors.</summary>
        public List<string> Errors { get; init; } = new();

        /// <summary>For diagnostics.</summary>
        public TraceLevel TraceLevel { get; set; } = TraceLevel.App;
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

            // Generate actual nodes and edges from states. TODO options to add func names etc.
            foreach (State s in _stateMap.Values)
            {
                // Write a node for the state.
                //ls.Add($"    \"{s.StateName}\";");

                // Iterate through the state transitions.
                foreach (KeyValuePair<string, Transition> kvp in s.TransitionMap)
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
        /// Init and validate the definitions.
        /// </summary>
        /// <param name="states">All the states.</param>
        /// <param name="initialState">Initial state.</param>
        /// <returns>Initialization success.</returns>
        protected bool InitSm(States states, string initialState)
        {
            Errors.Clear();
            _stateMap.Clear();
            _eventQueue.Clear();

            try // TODO patterns or simplify?
            {
                // Populate our collection from the client.
                foreach (State st in states)
                {



                    // Check for default state.
                    if (st.StateName == DEF_STATE)
                    {
                        if (_defaultState == null)
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
                        if (!_stateMap.ContainsKey(st.StateName))
                        {
                            _stateMap.Add(st.StateName, st);
                        }
                        else
                        {
                            Errors.Add($"Duplicate State Name:[{st.StateName}]");
                        }
                    }
                }

                if (_defaultState != null)
                {
                    _stateMap.Add(DEF_STATE, _defaultState);
                }

                // Initialize states and do sanity checking.
                List<string> keyList = new(_stateMap.Keys);

                foreach (State st in _stateMap.Values)
                {
                    var err = st.Init(keyList); // the check
                    Errors.AddRange(err);
                }

                if (initialState != DEF_STATE && _stateMap.ContainsKey(initialState))
                {
                    _currentState = _stateMap[initialState];
                    _currentState.Enter(null);
                }
                else // invalid initial state
                {
                    Errors.Add($"Invalid Initial State:[{initialState}]");
                }
            }
            catch (Exception e)
            {
                Errors.Add($"Exception during initializing SM:{e.Message} ({e.StackTrace})");
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
        protected bool ProcessEvent(string evt, object o = null) // TODO Trace for sm events
        {
            bool ok = true;

            lock (_locker)
            {
                // Add the event to the queue.
                _eventQueue.Enqueue(new EventInfo(evt, o));

                // Check for recursion through the processing loop - event may be generated internally during processing.
                if (!_processingEvents)
                {
                    _processingEvents = true;

                    // Process all events in the event queue. // TODO patterns or simplify?
                    while (_eventQueue.Count > 0 && ok)
                    {
                        EventInfo ei = _eventQueue.Dequeue();
                        try
                        {
                            // Dig out the correct transition if there is one.
                            string nextStateName = null;

                            // Try default state first.
                            if (nextStateName is null && _defaultState != null)
                            {
                                nextStateName = _defaultState.ProcessEvent(ei);
                            }

                            // No default state handler for this event, try current state.
                            if (nextStateName is null)
                            {
                                nextStateName = _currentState.ProcessEvent(ei);
                            }

                            // Ooops.
                            if (nextStateName is null)
                            {
                                throw new Exception($"State:[{_currentState.StateName}] Invalid event:[{ei.Name}]");
                            }

                            // Is there a state change?
                            if (nextStateName != _currentState.StateName)
                            {
                                // Get the next state.
                                State nextState = _stateMap[nextStateName];

                                // Exit current state.
                                _currentState.Exit(ei.Param);

                                // Set new state.
                                _currentState = nextState;

                                // Enter new state.
                                _currentState.Enter(ei.Param);
                            }
                        }
                        catch (Exception e) // TODO better run time handling - ask client?
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
            if ((TraceLevel & lvl) > 0)
            {
                Debug.WriteLine($"{DateTime.Now.ToString("yyyy'-'MM'-'dd HH':'mm':'ss.fff")} {lvl} {s}");
            }
        }
        #endregion
    }
}    