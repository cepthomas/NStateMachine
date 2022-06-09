using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NBagOfTricks.Slog;


namespace NStateMachine
{
    /// <summary>Agnostic core engine of the state machine.</summary>
    public class SmEngine<S, E> where S : Enum where E : Enum
    {
        #region Fields
        /// <summary>The original.</summary>
        protected States<S, E> _states = new();

        /// <summary>All the states.</summary>
        readonly Dictionary<S, State<S, E>> _stateMap = new();

        /// <summary>The current state.</summary>
        State<S, E> _currentState = new();

        /// <summary>The event queue.</summary>
        readonly Queue<EventInfo<S, E>> _eventQueue = new();

        /// <summary>Queue serializing access.</summary>
        readonly object _locker = new();

        /// <summary>Flag to handle recursion in event processing.</summary>
        bool _processingEvents = false;

        /// <summary>My logger.</summary>
        readonly Logger _logger = LogManager.CreateLogger("SmEngine");
        #endregion

        #region Properties
        /// <summary>Readable version of current state.</summary>
        public S CurrentState => _currentState.StateId;
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
                "    node [height=\"1\", width=\"1.5\", shape=\"ellipse\", fixedsize=\"true\", fontsize=\"10\", fontname=\"Arial\"];",
                "    edge [fontsize=\"10\", fontname=\"Arial\"];",
                ""
            };

            List<string> errors = new();

            if (_states is null || _states.Count == 0)
            {
                errors.Add($"State definitions bad.");
            }
            else
            {
                // Generate actual nodes and edges from states. Use original spec for this, not our adjusted runtime version.
                Dictionary<S, string> nodeIds = new();

                // Collect the state node info. Presumably duplicates and invalids have already been detected by InitSm().
                foreach (State<S, E> st in _states)
                {
                    string nid = $"N{nodeIds.Count}";
                    nodeIds.Add(st.StateId, nid);

                    string enFunc = GetFuncName(st, "EntryFunc");
                    if(enFunc != "")
                    {
                        enFunc = $"{enFunc}()\\n";
                    }
                    string exFunc = GetFuncName(st, "ExitFunc");
                    if (exFunc != "")
                    {
                        exFunc = $"\\n{exFunc}()";
                    }

                    string stDesc = $"    {nid} [label=\"{enFunc}{st.StateId}{exFunc}\"]";
                    ls.Add(stDesc);
                }

                ls.Add(""); // space, man.

                // Now connect them up.
                foreach (State<S, E> st in _states)
                {
                    // Iterate through the state transitions.
                    foreach (Transition<S, E> t in st.Transitions)
                    {
                        // Get func name if pertinent.
                        string tFunc = GetFuncName(t, "TransitionFunc");

                        if (tFunc != "")
                        {
                            tFunc = $"\\n{tFunc}()";
                        }

                        string eventName = $"{t.EventId}{tFunc}";

                        // Write an edge for the transition.
                        try
                        {
                            ls.Add($"    {nodeIds[st.StateId]} -> {nodeIds[t.NextState]} [label=\"{eventName}\"];");
                        }
                        catch (Exception e) // This should never happen but just in case.
                        {
                            errors.Add(e.Message);
                        }
                    }
                }
            }

            if (errors.Count > 0)
            {
                ls.Add("");
                ls.Add($"    NERR [shape=\"rect\", color=\"red\", fixedsize=\"false\", label=\"Bad Machine!");
                errors.ForEach(e => ls.Add($" {e}"));
                ls.Add($"\"]");
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
        protected List<string> InitSm(S initialState)
        {
            _stateMap.Clear();
            _eventQueue.Clear();
            List<string> errors = new();

            // Populate our collection from the client.
            foreach (State<S, E> st in _states)
            {
                // Sanity check for duplicate state names.
                if (!_stateMap.ContainsKey(st.StateId))
                {
                    _stateMap.Add(st.StateId, st);
                }
                else
                {
                    errors.Add($"Duplicate StateName[{st.StateId}]");
                }
            }

            // Errors in state inits?
            foreach (State<S, E> st in _stateMap.Values)
            {
                errors.AddRange(st.Init());
            }

            if (_stateMap.ContainsKey(initialState))
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
            _currentState.Enter();
        }

        /// <summary>
        /// Processes an event. Returns when event queue is empty.
        /// Events can be coming on different threads so this method is locked.
        /// </summary>
        /// <param name="evt">Incoming event.</param>
        /// <param name="o">Optional event data.</param>
        /// <returns>Ok or error.</returns>
        protected bool ProcessEvent(E evt, object? o = null)
        {
            bool ok = true;

            lock (_locker)
            {
                // Turn on for debugging sm workings.
                _logger.LogTrace($"ProcessEvent:{evt}:{o} in state:{_currentState.StateId}");

                // Add the event to the queue.
                _eventQueue.Enqueue(new EventInfo<S, E>(evt, o));

                // Check for recursion through the processing loop - event may be generated internally during processing.
                if (!_processingEvents)
                {
                    _processingEvents = true;

                    // Process all events in the event queue.
                    while (_eventQueue.Count > 0 && ok)
                    {
                        EventInfo<S, E> ei = _eventQueue.Dequeue();

                        S? nextStateId = default;
                        bool handled = false;

                        // Try current state.
                        var res = _currentState.ProcessEvent(ei);
                        if(res.handled)
                        {
                            handled = true;
                            nextStateId = res.state;
                        }
                        // Try default state.
                        else if (_stateMap.ContainsKey(Common<S, E>.DEFAULT_STATE_ID))
                        {
                            res = _stateMap[Common<S, E>.DEFAULT_STATE_ID].ProcessEvent(ei);
                            if (res.handled)
                            {
                                handled = true;
                                nextStateId = res.state;
                            }
                        }

                        if (handled)
                        {
                            // Is there a state change?
                            if(nextStateId is not null)
                            {
                                if (nextStateId.CompareTo(_currentState.StateId) != 0)
                                {
                                    State<S, E> nextState = _stateMap[nextStateId];
                                    _currentState.Exit(ei.Param);
                                    _currentState = nextState;
                                    _currentState.Enter(ei.Param);
                                }
                            }
                        }
                        else
                        {
                            ok = false;
                            _eventQueue.Clear();
                            _logger.LogError($"Runtime Unhandled event:{ei.EventId} in state:{_currentState.StateId}");
                        }
                    }
                }

                // Done for now.
                _processingEvents = false;

                return ok;
            }
        }

        /// <summary>
        /// Get the instance name of a SmFunc property.
        /// </summary>
        /// <param name="o">The instance object.</param>
        /// <param name="prop">Which property.</param>
        /// <returns>The name or empty if not available.</returns>
        static string GetFuncName(object o, string prop)
        {
            var sf = o.GetType().GetProperty(prop);
            var fn = sf!.GetValue(o, null);
            string funcname = fn is not null ? $":{((SmFunc)fn).Method.Name}" : "";
            return funcname;
        }
        #endregion
    }
}    