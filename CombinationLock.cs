using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NStateMachine
{
    /// <summary>The CombinationLock class provides both an example and a test of the state machine classes.</summary>
    public class CombinationLock : SmEngine
    {
        /// <summary>Specify the state machine functionality.</summary>
        void CreateMap()
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
                       { DEF_EVENT,         SAME_STATE,     ClearCurrentEntry } // ignore other events
                    }
                },
                {
                    "Unlocked", UnlockedEnter, NO_FUNC, new()
                    {
                       { "Reset",           "Locked",       ClearCurrentEntry },
                       { "SetCombo",        "SettingCombo", ClearCurrentEntry },
                       { DEF_EVENT,         SAME_STATE,     ClearCurrentEntry  } // ignore other events
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
                       { "Shutdown",        "Locked",       ResetAll },
                       { "Bar",             "Foo",          NO_FUNC }
                    }
                },
            };

            bool ok = InitSm(states, "Initial"); //TODO check
        }

        #region Enums - as needed by application
        /// <summary>Standard 12-key keypad: 0-9, *, and # keys.</summary>
        public enum Keys
        {
            Key_0 = '0', Key_1, Key_2, Key_3, Key_4, Key_5, Key_6, Key_7, Key_8, Key_9,
            Key_Reset = '*',
            Key_Set = '#',
            Key_Power = '!',
        }

        /// <summary>State of the hardware lock</summary>
        public enum HwLockState
        {
            HwIsLocked,
            HwIsUnlocked
        }
        #endregion

        #region Fields - as needed by application
        /// <summary>Current combination. Initial combination is: 000.</summary>
        readonly List<Keys> _combination = new() { Keys.Key_0, Keys.Key_0, Keys.Key_0 };

        /// <summary>Where we are in the entered sequence.</summary>
        readonly List<Keys> _currentEntry = new();

        /// <summary>Current state of the hardware Lock</summary>
        HwLockState _hwLockState = HwLockState.HwIsLocked;
        #endregion

        #region Public API - called from main application loop
        /// <summary>Normal constructor.</summary>
        public CombinationLock()
        {
            CreateMap();
        }

        /// <summary>Input from the keypad</summary>
        /// <param name="key">Key pressed on the keypad</param>
        public void PressKey(Keys key)
        {
            Trace(TraceLevel.App, $"KeyPressed:{key}");

            bool ok = key switch
            {
              Keys.Key_Reset    => ProcessEvent("Reset", key),
              Keys.Key_Set      => ProcessEvent("SetCombo", key),
              Keys.Key_Power    => ProcessEvent("Shutdown", key),
              _                 => ProcessEvent("DigitKeyPressed", key)
            };
        }

        /// <summary>Only for testing.</summary>
        public void InjectBadEvent()
        {
            ProcessEvent("NGEVENT", false);
        }
        #endregion

        #region Transition functions - private
        /// <summary>Initialize the lock</summary>
        void InitialEnter(object o)
        {
            Trace(TraceLevel.App, $"InitialEnter:{o}");
            ProcessEvent(_hwLockState == HwLockState.HwIsLocked ? "IsLocked" : "IsUnlocked");
        }

        /// <summary>Dummy function</summary>
        void InitialExit(object o)
        {
            Trace(TraceLevel.App, $"InitialExit:{o}");
        }

        /// <summary>Locked transition function.</summary>
        void LockedEnter(object o)
        {
            Trace(TraceLevel.App, $"LockedEnter:{o}");
            _hwLockState = HwLockState.HwIsLocked;
            _currentEntry.Clear();
        }

        /// <summary>Clear the lock</summary>
        void ClearCurrentEntry(object o)
        {
            Trace(TraceLevel.App, $"ClearCurrentEntry:{o}");
            _currentEntry.Clear();
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void LockedAddDigit(object o)
        {
            Trace(TraceLevel.App, $"LockedAddDigit:{o}");
            Keys key = (Keys)o;

            _currentEntry.Add(key);
            if (_currentEntry.SequenceEqual(_combination))
            {
                ProcessEvent("ValidCombo");
            }
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void SetComboAddDigit(object o)
        {
            Trace(TraceLevel.App, $"SetComboAddDigit:{o}");
            Keys key = (Keys)o;
            _currentEntry.Add(key);
        }

        /// <summary>Try setting a new combination.</summary>
        void SetCombo(object o)
        {
            Trace(TraceLevel.App, $"SetCombo:{o}");
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
            Trace(TraceLevel.App, $"UnlockedEnter:{o}");
            _hwLockState = HwLockState.HwIsUnlocked;
        }

        /// <summary>Clear the lock.</summary>
        void ResetAll(object o)
        {
            Trace(TraceLevel.App, $"ClearCurrentEntry:{o}");
            _hwLockState = HwLockState.HwIsLocked;
            _currentEntry.Clear();
        }

        /// <summary>Cause an exception to be thrown.</summary>
        void ForceFail(object o)
        {
            Trace(TraceLevel.App, "ForceFail");
            throw new Exception("ForceFail");
        }
        #endregion
    }
}