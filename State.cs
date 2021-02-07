using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>Describes an individual state. See README.md.</summary>
    public class State
    {
        #region Properties
        /// <summary>The state name.</summary>
        public string StateName { get; internal set; } = "???";

        /// <summary>Optional state entry action.</summary>
        public SmFunc EntryFunc { get; init; } = null;

        /// <summary>Optional state exit action.</summary>
        public SmFunc ExitFunc { get; init; } = null;

        /// <summary>All the transitions possible for this state. Only used for initialization.</summary>
        public Transitions Transitions { get; init; } = null;

        /// <summary>Massaged runtime version of Transitions. Key is event name.</summary>
        public Dictionary<string, Transition> TransitionMap { get; init; } = new();
        #endregion

        #region Fields
        /// <summary>Convenience reference to optional default transition.</summary>
        private Transition _defaultTransition = null;
        #endregion

        #region Public functions
        /// <summary>Initialize the state and its transitions.</summary>
        /// <param name="stateNames">All valid state names</param>
        /// <returns>List of any errors.</returns>
        public List<string> Init(List<string> stateNames)
        {
            List<string> errors = new();

            // Basic sanity check.
            if (Transitions.Count == 0)
            {
                errors.Add($"No transitions for State:[{StateName}]");
            }

            // Adjust transitions for DEFAULT_EVENT and SAME_STATE values.

            // Copy the transitions temporarily, ignoring the event names for now.
            Dictionary<string, Transition> tempTrans = new();
            Transitions.ForEach(t => { tempTrans.Add(tempTrans.Count.ToString(), t); });

            foreach (Transition t in tempTrans.Values)
            {

                //Tuple patterns
                string first = "ddd";
                string second = "eee";
                string s = (first, second) switch
                {
                    ("rock", "paper") => "rock is covered by paper. Paper wins.",
                    ("rock", "scissors") => "rock breaks scissors. Rock wins.",
                    ("paper", "rock") => "paper covers rock. Paper wins.",
                    ("paper", "scissors") => "paper is cut by scissors. Scissors wins.",
                    ("scissors", "rock") => "scissors is broken by rock. Rock wins.",
                    ("scissors", "paper") => "scissors cuts paper. Scissors wins.",
                    (_, _) => "tie"
                };

                //Keys key
                //bool ok = key switch
                //{
                //    Keys.Key_Reset => ProcessEvent("Reset", key),
                //    Keys.Key_Set => ProcessEvent("SetCombo", key),
                //    Keys.Key_Power => ProcessEvent("Shutdown", key),
                //    _ => ProcessEvent("DigitKeyPressed", key)
                //};


                //public static T ExhaustiveExample<T>(IEnumerable<T> sequence) =>
                //List<string> sequence = new();
                //var v = sequence switch
                //{
                //    Array { Length: 0 } => default(T),
                //    Array { Length: 1 } array => (T)array.GetValue(0),
                //    Array { Length: 2 } array => (T)array.GetValue(1),
                //    Array array => (T)array.GetValue(2),
                //    IEnumerable<T> list when !list.Any() => default(T),
                //    IEnumerable<T> list when list.Count() < 3 => list.Last(),
                //    IList<T> list => list[2],
                //    null => throw new ArgumentNullException(nameof(sequence)),
                //    _ => sequence.Skip(2).First(),
                //};
                //The preceding example adds a null pattern, and changes the IEnumerable<T> type pattern to a _ pattern.
                //The null pattern provides a null check as a switch expression arm.The expression for that arm throws an
                //ArgumentNullException.The _ pattern matches all inputs that haven't been matched by previous arms. It must
                //come after the null check, or it would match null inputs.





                // Handle default condition. TODO patterns or simplify?
                if (t.EventName == SmEngine.DEF_STATE)
                {
                    if (_defaultTransition is null)
                    {
                        _defaultTransition = t;
                    }
                    else
                    {
                        errors.Add($"Duplicate Default Event defined for:{StateName}");
                    }
                }
                else
                {
                    // Add to final map.
                    if (!TransitionMap.ContainsKey(t.EventName))
                    {
                        TransitionMap.Add(t.EventName, t);
                    }
                    else
                    {
                        errors.Add($"Duplicate Event Name:{t.EventName}");
                    }
                }

                // Fix any SAME_STATE to current.
                if(t.NextState == SmEngine.SAME_STATE)
                {
                    t.NextState = StateName;
                }

                // Is the nextState valid?
                if (!stateNames.Contains(t.NextState))
                {
                    errors.Add($"Undefined NextState:{ t.NextState}");
                }
            }

            return errors;
        }

        /// <summary>Process the event. Execute transition if found, otherwise return null and let the caller handle it.</summary>
        /// <param name="ei">The event information.</param>
        /// <returns>The next state name.</returns>
        public string ProcessEvent(EventInfo ei)
        {
            // Get the transition associated with the event or default.
            var tx = TransitionMap.GetValueOrDefault(ei.Name, _defaultTransition);
            return tx?.Execute(ei);
        }

        /// <summary>Enter the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        public void Enter(object o) => EntryFunc?.Invoke(o);

        /// <summary>Exit the state by executing the enter action</summary>
        /// <param name="o">Optional data object</param>
        public void Exit(object o) => ExitFunc?.Invoke(o);
        #endregion
    }

    /// <summary>Specialized container. Has Add() to support cleaner initialization.</summary>
    public class States : List<State>
    {
        public void Add(string stn, SmFunc entry, SmFunc exit, Transitions transitions) =>
           Add(new() { StateName = stn, EntryFunc = entry, ExitFunc = exit, Transitions = transitions });
    }
}    