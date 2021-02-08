using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace NStateMachine
{
    public class LockTest
    {
        Lock _lock = null;

        public void Run()
        {
            // Create a new combo lock.
            _lock = new();
            _lock.TraceLevel = TraceLevel.ALL;
            IsEqual(_lock.Init(), 1); // There is one syntax error.

            // Should come up in the locked state.
            IsEqual(_lock.CurrentState, "Locked");

            // Enter the default combination of 000.
            _lock.PressKey(Lock.Keys.Key_0);
            IsEqual(_lock.CurrentState, "Locked");
            _lock.PressKey(Lock.Keys.Key_0);
            IsEqual(_lock.CurrentState, "Locked");
            _lock.PressKey(Lock.Keys.Key_0);
            // Should now be unlocked.
            IsEqual(_lock.CurrentState, "Unlocked");

            // Test the default handler. Should stay in the same state.
            _lock.PressKey(Lock.Keys.Key_5);
            IsEqual(_lock.CurrentState, "Unlocked");

            // Lock it again.
            _lock.PressKey(Lock.Keys.Key_Reset);
            IsEqual(_lock.CurrentState, "Locked");

            // Unlock it again.
            _lock.PressKey(Lock.Keys.Key_0);
            _lock.PressKey(Lock.Keys.Key_0);
            _lock.PressKey(Lock.Keys.Key_0);
            IsEqual(_lock.CurrentState, "Unlocked");

            // Must be in the unlocked state to change the combination.
            // Press set, new combo, set, set the combination to 123.
            _lock.PressKey(Lock.Keys.Key_Set);
            IsEqual(_lock.CurrentState, "SettingCombo");

            _lock.PressKey(Lock.Keys.Key_1);
            _lock.PressKey(Lock.Keys.Key_2);
            _lock.PressKey(Lock.Keys.Key_3);
            IsEqual(_lock.CurrentState, "SettingCombo");

            _lock.PressKey(Lock.Keys.Key_Set);
            IsEqual(_lock.CurrentState, "Unlocked");

            // Default state test.
            _lock.PressKey(Lock.Keys.Key_Reset);
            IsEqual(_lock.CurrentState, "Locked");
            _lock.PressKey(Lock.Keys.Key_Power);
            IsEqual(_lock.CurrentState, "Locked");

            _lock.InjectBadEvent();
            IsEqual(_lock.CurrentState, "DEF_STATE");
            // The state machine is now dead and will no longer process events.

            // Make a picture.
            string sdot = _lock.GenerateDot();
            File.WriteAllText("testout.gv", sdot);
            using Process p = new();
            p.StartInfo.FileName = "dot";
            p.StartInfo.Arguments = "-Tpng testout.gv -o testout.png";
            bool ok = p.Start();
        }

        /// <summary>Checker.</summary>
        void IsEqual<T>(T value1, T value2, [CallerFilePath] string file = "???", [CallerLineNumber] int line = -1) where T : IComparable
        {
            if (value1.CompareTo(value2) != 0)
            {
                string s = $"FAIL [{value1}] should be [{value2}] : {file}({line})";
                _lock.Trace(TraceLevel.TESTF, s);
                Console.WriteLine(s);
            }
        }
    }
}