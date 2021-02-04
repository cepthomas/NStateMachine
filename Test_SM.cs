using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NStateMachine
{
    public class SM_FULL_TEST
    {
        public void RunSuite()
        {
            UT_INFO("Test the full StateMachine using a real world example.");

            // Create a new lock.
            CombinationLock mainDoorLock = new CombinationLock(CombinationLock.HwLockStates.HwIsLocked);
            mainDoorLock.InitStateMachine();

            // Should come up in the locked state.
            UT_EQUAL(mainDoorLock.CurrentState, "Locked");

            // Enter the default combination of 000.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            UT_EQUAL(mainDoorLock.CurrentState, "Locked");
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            UT_EQUAL(mainDoorLock.CurrentState, "Locked");
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);

            // Should now be unlocked.
            UT_EQUAL(mainDoorLock.CurrentState, "Unlocked");

            // Test the default handler. Should stay in the same state.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_5);
            UT_EQUAL(mainDoorLock.CurrentState, "Unlocked");

            // Lock it again.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_Reset);
            UT_EQUAL(mainDoorLock.CurrentState, "Locked");

            // Unlock it again.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            UT_EQUAL(mainDoorLock.CurrentState, "Unlocked");

            // Must be in the unlocked state to change the combination.
            // Press set, new combo, set, set the combination to 123.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_Set);
            UT_EQUAL(mainDoorLock.CurrentState, "SettingCombo");

            UT_EQUAL(mainDoorLock.SM.ProcessEvent("NGEVENT"), false);

            UT_EQUAL(mainDoorLock.CurrentState, "SettingCombo");

            // The state machine is now dead and will no longer process events.
            UT_GREATER(mainDoorLock.SM.Errors.Count, 0);

            mainDoorLock.PressKey(CombinationLock.Keys.Key_1);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_2);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_3);
            UT_EQUAL(mainDoorLock.CurrentState, "SettingCombo");

            mainDoorLock.PressKey(CombinationLock.Keys.Key_Set);

            UT_EQUAL(mainDoorLock.CurrentState, "Unlocked");

            // Default state test.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_Power);

            UT_EQUAL(mainDoorLock.CurrentState, "Locked");

            // Make a picture, maybe.
            try
            {
                string sdot = mainDoorLock.SM.GenerateDot();
                File.WriteAllText("testout.gv", sdot);
                Process p = new Process();
                p.StartInfo.FileName = "dot";
                p.StartInfo.Arguments = "-Tpng testout.gv -o testout.png";
                bool ok = p.Start();
            }
            catch (Exception)
            {
            }
        }

        void UT_INFO(string message, params object[] vars)
        {
            Console.WriteLine($"{message} {string.Join(", ", vars)}");
        }

        void UT_EQUAL<T>(T value1, T value2, [CallerFilePath] string file = "???", [CallerLineNumber] int line = -1) where T : IComparable
        {
            if (value1.CompareTo(value2) == 0)
            {
                Console.WriteLine($"[{value1}] should be [{value2}] : {file}({line})");
            }
        }
        void UT_GREATER<T>(T value1, T value2, [CallerFilePath] string file = "???", [CallerLineNumber] int line = -1) where T : IComparable
        {
            if (value1.CompareTo(value2) != 1)
            {
                Console.WriteLine($"[{value1}] should be greater than [{value2}] : {file}({line})");
            }
        }
    }

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

        /// <summary>Accessor to the tested StateMachine.</summary>
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
            //string st, SmFunc entry, SmFunc exit, List<Transition> transitions
            //string evt, string nextState = "", SmFunc transFunc = null

            //States ss = new States();
            //ss.Add()

            States states = new States()
            {
                {
                    "Initial", InitialEnter, InitialExit, new Transitions()
                    {
                        { "IsLocked", "Locked" },
                        { "IsUnlocked", "Unlocked" }
                    }
                },
                {
                    "Locked", LockedEnter, null, new Transitions()
                    {
                       { "ForceFail", null, ForceFail },
                       { "DigitKeyPressed", null, LockedAddDigit },
                       { "Reset", null, ClearCurrentEntry },
                       { "ValidCombo", "Unlocked" },
                       { null, null, ClearCurrentEntry }// ignore other events
                    }
                },
                {
                    "Unlocked", UnlockedEnter, null, new Transitions()
                    {
                       { "Reset", "Locked", ClearCurrentEntry },
                       { "SetCombo", "SettingCombo", ClearCurrentEntry },
                       { null, null, ClearCurrentEntry  } // ignore other events
                    }
                },
                {
                    "SettingCombo", ClearCurrentEntry, null, new Transitions()
                    {
                        { "DigitKeyPressed", null, SetComboAddDigit },
                        { "SetCombo", "Unlocked", SetCombo },
                        { "Reset", "Unlocked", ClearCurrentEntry },
                    }
                },
                {
                    null, null, null, new Transitions()
                    {
                       { "Shutdown", "Locked", TryDefault },
                       { "Bar", "Foo" }
                    }
                },

            };

            // initialize the state machine
            bool stateMachineIsValid = SM.Init(states, "Initial");
        }




        //public void InitStateMachine()
        //{
        //    State[] states = new State[]
        //    {
        //        new State("Initial", InitialEnter, InitialExit,
        //            new Transition("IsLocked", "Locked"),
        //            new Transition("IsUnlocked", "Unlocked")),

        //        new State("Locked", LockedEnter, null,
        //            new Transition("ForceFail", null, ForceFail),
        //            new Transition("DigitKeyPressed", null, LockedAddDigit),
        //            new Transition("Reset", null, ClearCurrentEntry),
        //            new Transition("ValidCombo", "Unlocked"),
        //            new Transition(null, null, ClearCurrentEntry)), // ignore other events
                
        //        new State("Unlocked", UnlockedEnter, null,
        //            new Transition("Reset", "Locked", ClearCurrentEntry),
        //            new Transition("SetCombo", "SettingCombo", ClearCurrentEntry),
        //            new Transition(null, null, ClearCurrentEntry)), // ignore other events
                
        //        new State("SettingCombo", ClearCurrentEntry, null,
        //            new Transition("DigitKeyPressed", null, SetComboAddDigit),
        //            new Transition("SetCombo", "Unlocked", SetCombo),
        //            new Transition("Reset", "Unlocked", ClearCurrentEntry)),

        //        new State(null, null, null,
        //            new Transition("Shutdown", "Locked", TryDefault),
        //            new Transition("Bar", "Foo"))
        //    };

        //    // initialize the state machine
        //    bool stateMachineIsValid = SM.Init(states, "Initial");
        //}
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
        public enum HwLockStates
        {
            HwIsLocked,
            HwIsUnlocked
        }
        #endregion

        #region Fields
        /// <summary>Current state of the hardware Lock</summary>
        HwLockStates _hwLockState;
        #endregion

        #region Private functions
        /// <summary>Energize the hardware lock to the locked position</summary>
        void HwLock()
        {
            Trace("HwLock: Locking");
            _hwLockState = HwLockStates.HwIsLocked;
        }

        /// <summary>Energize the hardware lock to the unlocked position</summary>
        void HwUnLock()
        {
            Trace("HwLock: Unlocking");
            _hwLockState = HwLockStates.HwIsUnlocked;
        }

        /// <summary>Adjust to taste.</summary>
        /// <param name="s"></param>
        void Trace(string s)
        {
            // Console.WriteLine(s);
        }
        #endregion

        #region Construction
        /// <summary>Normal constructor.</summary>
        /// <param name="hwLockState">Initialize state</param>
        public CombinationLock(HwLockStates hwLockState)
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
        void InitialEnter(Object o)
        {
            Trace($"InitialEnter:{o}");
            if (_hwLockState == HwLockStates.HwIsLocked)
            {
                SM.ProcessEvent("IsLocked");
            }
            else
            {
                SM.ProcessEvent("IsUnlocked");
            }
        }

        /// <summary>Dummy function</summary>
        void InitialExit(Object o)
        {
            Trace($"InitialExit:{o}");
        }

        /// <summary>Locked transition function.</summary>
        void LockedEnter(Object o)
        {
            Trace($"LockedEnter:{o}");
            HwLock();
            _currentEntry.Clear();
        }

        /// <summary>Clear the lock</summary>
        void ClearCurrentEntry(Object o)
        {
            Trace($"ClearCurrentEntry:{o}");
            _currentEntry.Clear();
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void LockedAddDigit(Object o)
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
        void SetComboAddDigit(Object o)
        {
            Trace($"SetComboAddDigit:{o}");
            Keys key = (Keys)o;

            _currentEntry.Add(key);
        }

        /// <summary>Try setting a new combination.</summary>
        void SetCombo(Object o)
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
        void UnlockedEnter(Object o)
        {
            Trace($"UnlockedEnter:{o}");
            HwUnLock();
        }

        /// <summary>Cause an exception to be thrown.</summary>
        void ForceFail(Object o)
        {
            Trace("ForceFail");
            throw new Exception("ForceFail");
        }

        /// <summary>Clear the lock</summary>
        void TryDefault(Object o)
        {
            Trace($"ClearCurrentEntry:{o}");
            HwLock();
            _currentEntry.Clear();
        }
        #endregion
    }
}