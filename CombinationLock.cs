using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NStateMachine
{
    /// <summary>The CombinationLock class provides both an example and a test of the state machine classes.</summary>
    public class CombinationLock
    {
        #region Private fields
        /// <summary>Current combination.</summary>
        List<Keys> _combination = new List<Keys>();

        /// <summary>Where we are in the sequence.</summary>
        List<Keys> _currentEntry = new List<Keys>();
        #endregion

        #region Test support public
        /// <summary>Readable version of current state for testing.</summary>
        public string CurrentState { get { return SM.CurrentState.StateName; } }

        /// <summary>Accessor to the engine.</summary>
        public SmEngine SM { get; set; }

        /// <summary>Input from the keypad</summary>
        /// <param name="key">Key pressed on the keypad</param>
        public void PressKey(Keys key)
        {
            Trace($"KeyPressed:{key}");

            switch (key)
            {
                case Keys.Key_Reset:
                    SM.ProcessEvent("Reset", key);
                    break;

                case Keys.Key_Set:
                    SM.ProcessEvent("SetCombo", key);
                    break;

                case Keys.Key_Power:
                    SM.ProcessEvent("Shutdown", key);
                    break;

                default:
                    SM.ProcessEvent("DigitKeyPressed", key);
                    break;
            }
        }
        #endregion

        #region The State Machine
        /// <summary>Initialize the state machine.</summary>
        public void InitStateMachine()
        {
            States states = new States()
            {
                {
                    "Initial", InitialEnter, InitialExit, new Transitions()
                    {
                        { "IsLocked",       "Locked" },
                        { "IsUnlocked",     "Unlocked" }
                    }
                },
                {
                    "Locked", LockedEnter, null, new Transitions()
                    {
                       { "ForceFail",       null,       ForceFail },
                       { "DigitKeyPressed", null,       LockedAddDigit },
                       { "Reset",           null,       ClearCurrentEntry },
                       { "ValidCombo",      "Unlocked" },
                       { null,              null,       ClearCurrentEntry } // ignore other events
                    }
                },
                {
                    "Unlocked", UnlockedEnter, null, new Transitions()
                    {
                       { "Reset",           "Locked",           ClearCurrentEntry },
                       { "SetCombo",        "SettingCombo",     ClearCurrentEntry },
                       { null,              null,               ClearCurrentEntry  } // ignore other events
                    }
                },
                {
                    "SettingCombo", ClearCurrentEntry, null, new Transitions()
                    {
                        { "DigitKeyPressed", null,          SetComboAddDigit },
                        { "SetCombo",       "Unlocked",     SetCombo },
                        { "Reset",          "Unlocked",     ClearCurrentEntry },
                    }
                },
                {
                    null, null, null, new Transitions()
                    {
                       { "Shutdown",        "Locked",       TryDefault },
                       { "Bar",             "Foo" }
                    }
                },

            };

            // initialize the state machine
            bool stateMachineIsValid = SM.Init(states, "Initial");
        }
        #endregion

        #region Enums
        /// <summary>Standard 12-key keypad: 0-9, *, and # keys.</summary>
        public enum Keys
        {
            Key_0 = '0',
            Key_1,
            Key_2,
            Key_3,
            Key_4,
            Key_5,
            Key_6,
            Key_7,
            Key_8,
            Key_9,
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

        #region Fields
        /// <summary>Current state of the hardware Lock</summary>
        HwLockState _hwLockState;
        #endregion

        #region Private functions
        /// <summary>Energize the hardware lock to the locked position</summary>
        void HwLock()
        {
            Trace("HwLock: Locking");
            _hwLockState = HwLockState.HwIsLocked;
        }

        /// <summary>Energize the hardware lock to the unlocked position</summary>
        void HwUnLock()
        {
            Trace("HwLock: Unlocking");
            _hwLockState = HwLockState.HwIsUnlocked;
        }

        /// <summary>Adjust to taste.</summary>
        /// <param name="s"></param>
        void Trace(string s) //TODO put in sm?
        {
            // Console.WriteLine(s);
        }
        #endregion

        #region Construction
        /// <summary>Normal constructor.</summary>
        /// <param name="hwLockState">Initialize state</param>
        public CombinationLock(HwLockState hwLockState)
        {
            // Create the FSM.
            SM = new SmEngine();

            _hwLockState = hwLockState; // initialize the state of the hardware lock

            _currentEntry = new List<Keys>();

            // initial combination is: 000
            _combination = new List<Keys>
            {
                Keys.Key_0,
                Keys.Key_0,
                Keys.Key_0
            };
        }
        #endregion

        #region Transition functions
        /// <summary>Initialize the lock</summary>
        void InitialEnter(object o)
        {
            Trace($"InitialEnter:{o}");
            if (_hwLockState == HwLockState.HwIsLocked)
            {
                SM.ProcessEvent("IsLocked");
            }
            else
            {
                SM.ProcessEvent("IsUnlocked");
            }
        }

        /// <summary>Dummy function</summary>
        void InitialExit(object o)
        {
            Trace($"InitialExit:{o}");
        }

        /// <summary>Locked transition function.</summary>
        void LockedEnter(object o)
        {
            Trace($"LockedEnter:{o}");
            HwLock();
            _currentEntry.Clear();
        }

        /// <summary>Clear the lock</summary>
        void ClearCurrentEntry(object o)
        {
            Trace($"ClearCurrentEntry:{o}");
            _currentEntry.Clear();
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void LockedAddDigit(object o)
        {
            Trace($"LockedAddDigit:{o}");
            Keys key = (Keys)o;

            _currentEntry.Add(key);
            if (_currentEntry.SequenceEqual(_combination))
            {
                SM.ProcessEvent("ValidCombo");
            }
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void SetComboAddDigit(object o)
        {
            Trace($"SetComboAddDigit:{o}");
            Keys key = (Keys)o;

            _currentEntry.Add(key);
        }

        /// <summary>Try setting a new combination.</summary>
        void SetCombo(object o)
        {
            Trace($"SetCombo:{o}");
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
            Trace($"UnlockedEnter:{o}");
            HwUnLock();
        }

        /// <summary>Cause an exception to be thrown.</summary>
        void ForceFail(object o)
        {
            Trace("ForceFail");
            throw new Exception("ForceFail");
        }

        /// <summary>Clear the lock</summary>
        void TryDefault(object o)
        {
            Trace($"ClearCurrentEntry:{o}");
            HwLock();
            _currentEntry.Clear();
        }
        #endregion
    }
}