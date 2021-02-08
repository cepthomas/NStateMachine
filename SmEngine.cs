using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace NStateMachine
{
    /// <summary>Definition for transition/entry/exit functions.</summary>
    /// <param name="o">Optional data.</param>
    public delegate void SmFunc(object o);

    /// <summary>Data carrying class.</summary>
    public record EventInfo(string Name, object Param);

    /// <summary>For tracing.</summary>
    public record LogInfo(string LogType, DateTime TimeStamp, string Msg);


    /// <summary>Agnostic core engine of the state machine.</summary>
    public class SmEngine
    {
        #region Constants to make maps prettier
        public const SmFunc NO_FUNC = null;
        public const string DEF_STATE = "DEF_STATE";
        public const string SAME_STATE = "SAME_STATE";
        public const string DEF_EVENT = "DEF_EVENT";
        #endregion

        #region Logging
        /// <summary>Log me please.</summary>
        public event EventHandler<LogInfo> LogEvent;

        const string SM_LOG_CAT = "ENGRT";
        #endregion

        #region Fields
        /// <summary?The original.</summary>
        protected States _states = null;

        /// <summary>All the states.</summary>
        readonly Dictionary<string, State> _stateMap = new();

        /// <summary>The default state if used.</summary>
        State _defaultState = null;

        /// <summary>The current state.</summary>
        State _currentState = null;

        /// <summary>The event queue.</summary>
        readonly Queue<EventInfo> _eventQueue = new();

        /// <summary>Queue serializing access.</summary>
        readonly object _locker = new();

        /// <summary>Flag to handle recursion in event processing.</summary>
        bool _processingEvents = false;
        #endregion

        #region Properties
        /// <summary>Readable version of current state.</summary>
        public string CurrentState => _currentState is null ? "" : _currentState.StateName;
        #endregion

        #region Public functions
        /// <summary>
        /// Generate DOT markup and create a picture.
        /// </summary>
        /// <returns>Returns a string that contains the DOT markup.</returns>
        public string GenerateDot(string label)
        {
            List<string> ls = new()
            {
                // Init attributes for dot.
                "digraph StateDiagram {",
                "    ratio=\"compress\";",
                "    fontname=\"Arial\";",
                "    label=\"" + label + "\";",
                "",
                "    node [",
                "    height=\"1\";",
                "    width=\"2\";",
                "    shape=\"ellipse\";",
                "    fixedsize=\"true\";",
                "    fontsize=\"10\";",
                "    fontname=\"Arial\";",
                "    ];",
                "",
                "    edge [",
                "    fontsize=\"10\";",
                "    fontname=\"Arial\";",
                "    ];",
                ""
            };

            if (_states is null || _states.Count == 0)
            {
                ls.Add($"Bad Machine");
            }
            else
            {
                // Generate actual nodes and edges from states. Use original spec for this, not our adjusted runtime version.
                foreach (State st in _states)
                {
                    // Iterate through the state transitions.
                    foreach (Transition t in st.Transitions)
                    {
                        // Get func name if pertinent.
                        var sf = t.GetType().GetProperty("TransitionFunc");
                        var fn = sf.GetValue(t, null);
                        string funcname = fn is not null ? $":{(fn as SmFunc).Method.Name}()" : "";
                        string eventName = $"{t.EventName}{funcname}";

                        // Write an edge for the transition
                        ls.Add($"    \"{st.StateName}\" -> \"{t.NextState}\" [label=\"{eventName}\"];");
                    }
                }
            }

            ls.Add("}");

            return string.Join(Environment.NewLine, ls);
        }
        #endregion

        #region Private and protected functions
        /// <summary>
        /// Init and validate the definitions.
        /// </summary>
        /// <param name="initialState">Initial state.</param>
        /// <returns>List of syntax errors.</returns>
        protected List<string> InitSm(string initialState)
        {
            _stateMap.Clear();
            _eventQueue.Clear();
            List<string> errors = new();

            // Populate our collection from the client.
            foreach (State st in _states)
            {
                // Check for default state.
                if (st.StateName is DEF_STATE)
                {
                    if (_defaultState == null)
                    {
                        _defaultState = st;
                    }
                    else
                    {
                        errors.Add($"Multiple Default States");
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
                        errors.Add($"Duplicate StateName[{st.StateName}]");
                    }
                }
            }

            if (_defaultState is not null)
            {
                _stateMap.Add(DEF_STATE, _defaultState);
            }

            // Initialize states and do sanity checking.
            List<string> keyList = new(_stateMap.Keys);

            // Errors in state inits?
            foreach (State st in _stateMap.Values)
            {
                errors.AddRange(st.Init(keyList));
            }

            if (initialState != DEF_STATE && _stateMap.ContainsKey(initialState))
            {
                _currentState = _stateMap[initialState];
            }
            else // invalid initial state
            {
                errors.Add($"Invalid Initial State[{initialState}]");
            }

            return errors;
        }

        /// <summary>
        /// Machine is good so start it up.
        /// </summary>
        protected void StartSm()
        {
            _currentState.Enter(null);
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
                Log(SM_LOG_CAT, $"ProcessEvent:{evt}:{o}");

                // Add the event to the queue.
                _eventQueue.Enqueue(new EventInfo(evt, o));

                // Check for recursion through the processing loop - event may be generated internally during processing.
                if (!_processingEvents)
                {
                    _processingEvents = true;

                    // Process all events in the event queue.
                    while (_eventQueue.Count > 0 && ok)
                    {
                        EventInfo ei = _eventQueue.Dequeue();
                        // Dig out the correct transition if there is one.
                        string nextStateName = null;

                        // Try current state.
                        nextStateName ??= _currentState.ProcessEvent(ei);

                        // Try default state.
                        nextStateName ??= _defaultState.ProcessEvent(ei);

                        // Ooops.
                        nextStateName ??= DEF_STATE;

                        // Is there a state change?
                        if (nextStateName != _currentState.StateName)
                        {
                            State nextState = _stateMap[nextStateName];
                            _currentState.Exit(ei.Param);
                            _currentState = nextState;
                            _currentState.Enter(ei.Param);
                        }
                    }
                }

                // Done for now.
                _processingEvents = false;

                return ok;
            }
        }

        /// <summary>Trace/logging function.</summary>
        /// <param name="cat">Printable category.</param>
        /// <param name="msg">What to add.</param>
        protected void Log(string cat, string msg)
        {
            LogEvent?.Invoke(this, new LogInfo(cat, DateTime.Now, msg));
        }
        #endregion
    }
}    