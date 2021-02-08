using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace NStateMachine
{
    /// <summary>Definition for transition/entry/exit functions.</summary>
    /// <param name="o">Optional data.</param>
    public delegate void SmFunc(object o);

    /// <summary>Logging.</summary>
    [Flags]
    public enum TraceLevel { NONE = 0x00, APPSM = 0x01, APPRT = 0x02, ENGRT = 0x04, TESTF = 0x08, OTHER = 0x10, ALL = 0xFFFF }

    /// <summary>Data carrying class.</summary>
    public record EventInfo(string Name, object Param);

    /// <summary>Agnostic core engine of a state machine.</summary>
    public class SmEngine
    {
        #region Constants to make maps prettier
        public const SmFunc NO_FUNC = null;
        public const string DEF_STATE = "DEF_STATE";
        public const string SAME_STATE = "SAME_STATE";
        public const string DEF_EVENT = "DEF_EVENT";
        #endregion

        #region Fields
        /// <summary?The original.</summary>
        States _states = null;

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

        /// <summary>State machine syntax errors.</summary>
        int _smErrors = 0;
        #endregion

        #region Properties
        /// <summary>Readable version of current state.</summary>
        public string CurrentState => _currentState == null ? "" : _currentState.StateName;

        /// <summary>For diagnostics.</summary>
        public TraceLevel TraceLevel { get; set; } = TraceLevel.NONE;
        #endregion

        #region Public functions
        /// <summary>Adjust to taste. Public so clients can inject traces.</summary>
        /// <param name="lvl">Filter</param>
        /// <param name="s">What to add.</param>
        public void Trace(TraceLevel lvl, string s)
        {
            if ((TraceLevel & lvl) > 0)
            {
                Debug.WriteLine($"{DateTime.Now:yyyy'-'MM'-'dd HH':'mm':'ss.fff} {lvl} {s}");
            }
        }

        /// <summary>
        /// Generate DOT markup.
        /// </summary>
        /// <returns>Returns a string that contains the DOT markup.</returns>
        public string GenerateDot()
        {
            List<string> ls = new()
            {
                // Init attributes for dot.
                "digraph StateDiagram {",
                "    ratio=\"compress\";",
                "    fontname=\"Arial\";",
                "    label=\"\";", // (your label here!)
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

            // Generate actual nodes and edges from states. Use original spec for this, not our adjusted runtime version.
            foreach (State st in _states)
            {
                // Iterate through the state transitions.
                foreach (Transition t in st.Transitions)
                {
                    // Get func name if pertinent.
                    var sf = t.GetType().GetProperty("TransitionFunc");
                    var fn = sf.GetValue(t, null);
                    string funcname = fn is not null ? $"\n{(fn as SmFunc).Method.Name}()" : "";
                    string eventName = $"{t.EventName}{funcname}";

                    // Write an edge for the transition
                    ls.Add($"        \"{st.StateName}\" -> \"{t.NextState}\" [label=\"{eventName}\"];");
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
        /// <param name="states">All the states.</param>
        /// <param name="initialState">Initial state.</param>
        /// <returns>Number of syntax errors.</returns>
        protected int InitSm(States states, string initialState)
        {
            _states = states;
            _smErrors = 0;
            _stateMap.Clear();
            _eventQueue.Clear();

            // Populate our collection from the client.
            foreach (State st in states)
            {
                // Check for default state.
                if (st.StateName == DEF_STATE)
                {
                    if (_defaultState == null)
                    {
                        _defaultState = st;
                    }
                    else
                    {
                        SmError($"Multiple Default States");
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
                        SmError($"Duplicate StateName[{st.StateName}]");
                    }
                }
            }

            if (_defaultState != null)
            {
                _stateMap.Add(DEF_STATE, _defaultState);
            }

            // Initialize states and do sanity checking.
            List<string> keyList = new(_stateMap.Keys);

            // Errors in state inits?
            foreach (State st in _stateMap.Values)
            {
                st.Init(keyList).ForEach(e => SmError(e));
            }

            if (initialState != DEF_STATE && _stateMap.ContainsKey(initialState))
            {
                _currentState = _stateMap[initialState];
                _currentState.Enter(null);
            }
            else // invalid initial state
            {
                SmError($"Invalid Initial State[{initialState}]");
            }

            return _smErrors;
        }

        /// <summary>
        /// Handler for syntax errors.
        /// </summary>
        /// <param name="s"></param>
        void SmError(string s)
        {
            Trace(TraceLevel.APPSM, s);
            _smErrors++;
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
                Trace(TraceLevel.ENGRT, $"ProcessEvent:{evt}:{o}");

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
        #endregion
    }
}    