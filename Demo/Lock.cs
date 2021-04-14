using System;
using System.Collections.Generic;


namespace NStateMachine
{
    /// <summary>My states.</summary>
    public enum S { Default = 0, Initial, Locked, Unlocked, SettingCombo, Failed };

    /// <summary>My events.</summary>
    public enum E { Default = 0, DigitKeyPressed, ForceFail, IsLocked, IsUnlocked, Reset, SetCombo, Shutdown, ValidCombo };

    /// <summary>An example state machine implementing a standard combination lock.</summary>
    public class Lock : SmEngine<S, E>
    {
        /// <summary>Specify the state machine functionality.</summary>
        /// <returns>List of syntax errors.</returns>
        void CreateMap()
        {
            _states = new()
            {
                { 
                    S.Initial, InitialEnter, InitialExit, new()
                    {
                        { E.IsLocked,           S.Locked,           null },
                        { E.IsUnlocked,         S.Unlocked,         null }
                    }
                },
                {
                    S.Locked, LockedEnter, null, new()
                    {
                       { E.ForceFail,           S.Locked,           ForceFail },
                       { E.DigitKeyPressed,     S.Locked,           LockedAddDigit },
                       { E.Reset,               S.Locked,           ClearCurrentEntry },
                       { E.ValidCombo,          S.Unlocked,         null },
                    }
                },
                {
                    S.Unlocked, UnlockedEnter, null, new()
                    {
                       { E.Reset,               S.Locked,           ClearCurrentEntry },
                       { E.SetCombo,            S.SettingCombo,     ClearCurrentEntry },
                       { E.Default,             S.Unlocked,         ClearCurrentEntry } // ignores other events
                    }
                },
                {
                    S.SettingCombo, ClearCurrentEntry, null, new()
                    {
                        { E.DigitKeyPressed,    S.SettingCombo,     SetComboAddDigit },
                        { E.SetCombo,           S.Unlocked,         SetCombo },
                        { E.Reset,              S.Unlocked,         ClearCurrentEntry },
                    }
                },
                {
                    S.Default, null, null, new()
                    {
                        { E.Shutdown,           S.Locked,           ResetAll },
                        { E.Default,            S.Default,          UnexpectedEvent }
                    }
                },
            };
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

        const string LOCK_LOG_CAT = "LOCK";
        #endregion

        #region Public API - called from main application loop
        /// <summary>Initialize the map.</summary>
        /// <returns>List of syntax errors.</returns>
        public List<string> Init()
        {
            CreateMap();
            var errors = InitSm(S.Initial);
            return errors;
        }

        /// <summary>
        /// Start the sm.
        /// </summary>
        public void Run()
        {
            StartSm();
        }

        /// <summary>Input from the keypad</summary>
        /// <param name="key">Key pressed on the keypad</param>
        public void PressKey(Keys key)
        {
            Log(LOCK_LOG_CAT, $"KeyPressed:{key}");

            _ = key switch
            {
                Keys.Key_Reset  => ProcessEvent(E.Reset, key),
                Keys.Key_Set    => ProcessEvent(E.SetCombo, key),
                Keys.Key_Power  => ProcessEvent(E.Shutdown, key),
                _               => ProcessEvent(E.DigitKeyPressed, key)
            };
        }
        #endregion

        #region Transition functions - private
        /// <summary>Initialize the lock</summary>
        void InitialEnter(object o)
        {
            Log(LOCK_LOG_CAT, $"InitialEnter:{o}");
            ProcessEvent(_isLocked ? E.IsLocked : E.IsUnlocked);
        }

        /// <summary>Dummy function</summary>
        void InitialExit(object o)
        {
            Log(LOCK_LOG_CAT, $"InitialExit:{o}");
        }

        /// <summary>Locked transition function.</summary>
        void LockedEnter(object o)
        {
            Log(LOCK_LOG_CAT, $"LockedEnter:{o}");
            _isLocked = true;
            _currentEntry.Clear();
        }

        /// <summary>Clear the lock</summary>
        void ClearCurrentEntry(object o)
        {
            Log(LOCK_LOG_CAT, $"ClearCurrentEntry:{o}");
            _currentEntry.Clear();
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void LockedAddDigit(object o)
        {
            Log(LOCK_LOG_CAT, $"LockedAddDigit:{o}");
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
                ProcessEvent(E.ValidCombo);
            }
        }

        /// <summary>Add a digit to the current sequence.</summary>
        void SetComboAddDigit(object o)
        {
            Log(LOCK_LOG_CAT, $"SetComboAddDigit:{o}");
            Keys key = (Keys)o;
            _currentEntry.Add(key);
        }

        /// <summary>Try setting a new combination.</summary>
        void SetCombo(object o)
        {
            Log(LOCK_LOG_CAT, $"SetCombo:{o}");
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
            Log(LOCK_LOG_CAT, $"UnlockedEnter:{o}");
            _isLocked = false;
        }

        /// <summary>Clear the lock.</summary>
        void ResetAll(object o)
        {
            Log(LOCK_LOG_CAT, $"ClearCurrentEntry:{o}");
            _isLocked = true;
            _currentEntry.Clear();
        }

        /// <summary>Cause an exception to be thrown.</summary>
        void ForceFail(object o)
        {
            Log(LOCK_LOG_CAT, "ForceFail");
            throw new Exception("ForceFail");
        }

        /// <summary>Runtime bad event. Do something app-specific.</summary>
        void UnexpectedEvent(object o)
        {
            Log(LOCK_LOG_CAT, "UnexpectedEvent");
            // maybe throw new Exception("UnexpectedEvent");
        }
        #endregion
    }
}