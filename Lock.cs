using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>An example state machine implementing a standard combination lock.</summary>
    public class Lock : SmEngine
    {
        /// <summary>Specify the state machine functionality.</summary>
        int CreateMap()
        {
            States states = new()
            {
                { "Initial", InitialEnter, InitialExit, new()
                    {
                        { "IsLocked",       "Locked",       NO_FUNC },
                        { "IsUnlocked",     "Unlocked",     NO_FUNC }
                    }
                },
                {
                    "Locked", LockedEnter, NO_FUNC, new()
                    {
                       { "ForceFail",       SAME_STATE,     ForceFail },
                       { "DigitKeyPressed", SAME_STATE,     LockedAddDigit },
                       { "Reset",           SAME_STATE,     ClearCurrentEntry },
                       { "ValidCombo",      "Unlocked",     NO_FUNC },
                    }
                },
                {
                    "Unlocked", UnlockedEnter, NO_FUNC, new()
                    {
                       { "Reset",           "Locked",       ClearCurrentEntry },
                       { "SetCombo",        "SettingCombo", ClearCurrentEntry },
                       { DEF_EVENT,         SAME_STATE,     ClearCurrentEntry } // ignore other events
                    }
                },
                {
                    "SettingCombo", ClearCurrentEntry, NO_FUNC, new()
                    {
                        { "DigitKeyPressed", SAME_STATE,    SetComboAddDigit },
                        { "SetCombo",       "Unlocked",     SetCombo },
                        { "Reset",          "Unlocked",     ClearCurrentEntry },
                    }
                },
                {
                    DEF_STATE, NO_FUNC, NO_FUNC, new()
                    {
                        { "Shutdown",       "Locked",       ResetAll },
                        { "Bar",            "Foo",          NO_FUNC },
                        { DEF_EVENT,        SAME_STATE,     UnexpectedEvent }
                    }
                },
            };

            return InitSm(states, "Initial");
        }

        #region Context data for application
        /// <summary>Standard keypad with control functions.</summary>
        public enum Keys
        {
            Key_0 = '0', Key_1, Key_2, Key_3, Key_4, Key_5, Key_6, Key_7, Key_8, Key_9,
            Key_Reset = '*',
            Key_Set = '#',
            Key_Power = '!',
        }

        /// <summary>Current combination. Initial combination is: 000.</summary>
        readonly List<Keys> _combination = new() { Keys.Key_0, Keys.Key_0, Keys.Key_0 };

        /// <summary>Where we are in the entered sequence.</summary>
        readonly List<Keys> _currentEntry = new();

        /// <summary>Current state of the lock.</summary>
        bool _isLocked = true;
        #endregion

        #region Public API - called from main application loop
        /// <summary>Initialize the map.</summary>
        /// <returns>Number of syntax errors.</returns>
        public int Init()
        {
            return CreateMap();
        }

        /// <summary>Input from the keypad</summary>
        /// <param name="key">Key pressed on the keypad</param>
        public void PressKey(Keys key)
        {
            Trace(TraceLevel.APPRT, $"KeyPressed:{key}");

            _ = key switch
            {
                Keys.Key_Reset  => ProcessEvent("Reset", key),
                Keys.Key_Set    => ProcessEvent("SetCombo", key),
                Keys.Key_Power  => ProcessEvent("Shutdown", key),
                _               => ProcessEvent("DigitKeyPressed", key)
            };
        }

        /// <summary>Only for testing.</summary>
        public void InjectBadEvent()
        {
            ProcessEvent("BAD_EVENT", false);
        }
        #endregion

        #region Transition functions - private
        /// <summary>Initialize the lock</summary>
        void InitialEnter(object o)
        {
            Trace(TraceLevel.APPRT, $"InitialEnter:{o}");
            ProcessEvent(_isLocked ? "IsLocked" : "IsUnlocked");
        }

        /// <summary>Dummy function</summary>
        void InitialExit(object o)
        {
            Trace(TraceLevel.APPRT, $"InitialExit:{o}");
        }

        /// <summary>Locked transition function.</summary>
        void LockedEnter(object o)
        {
            Trace(TraceLevel.APPRT, $"LockedEnter:{o}");
            _isLocked = true;
            _currentEntry.Clear();
        }

        /// <summary>Clear the lock</summary>
        void ClearCurrentEntry(object o)
        {
            Trace(TraceLevel.APPRT, $"ClearCurrentEntry:{o}");
            _currentEntry.Clear();
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void LockedAddDigit(object o)
        {
            Trace(TraceLevel.APPRT, $"LockedAddDigit:{o}");
            Keys key = (Keys)o;

            _currentEntry.Add(key);

            // Is the combination complete?
            bool valid = _currentEntry.Count == _combination.Count;
            for (int i = 0; i < _currentEntry.Count && valid; i++)
            {
                valid = _currentEntry[i] == _combination[i];
            }

            if(valid)
            {
                ProcessEvent("ValidCombo");
            }
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void SetComboAddDigit(object o)
        {
            Trace(TraceLevel.APPRT, $"SetComboAddDigit:{o}");
            Keys key = (Keys)o;
            _currentEntry.Add(key);
        }

        /// <summary>Try setting a new combination.</summary>
        void SetCombo(object o)
        {
            Trace(TraceLevel.APPRT, $"SetCombo:{o}");
            if (_currentEntry.Count > 0)
            {
                _combination.Clear();
                _combination.AddRange(_currentEntry);
                _currentEntry.Clear();
            }
        }

        /// <summary>Lock is unlocked now.</summary>
        void UnlockedEnter(object o)
        {
            Trace(TraceLevel.APPRT, $"UnlockedEnter:{o}");
            _isLocked = false;
        }

        /// <summary>Clear the lock.</summary>
        void ResetAll(object o)
        {
            Trace(TraceLevel.APPRT, $"ClearCurrentEntry:{o}");
            _isLocked = true;
            _currentEntry.Clear();
        }

        /// <summary>Cause an exception to be thrown.</summary>
        void ForceFail(object o)
        {
            Trace(TraceLevel.APPRT, "ForceFail");
            throw new Exception("ForceFail");
        }

        /// <summary>Runtime bad event. Do something app-specific.</summary>
        void UnexpectedEvent(object o)
        {
            Trace(TraceLevel.APPRT, "UnexpectedEvent");
            //throw new Exception("UnexpectedEvent");
        }
        #endregion
    }
}