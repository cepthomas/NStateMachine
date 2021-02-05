using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NStateMachine
{
    public class TestLock
    {
        public void Run()
        {
            // Create a new lock.
            CombinationLock mainDoorLock = new CombinationLock(CombinationLock.HwLockState.HwIsLocked);
            mainDoorLock.InitStateMachine();

            // Should come up in the locked state.
            IsEqual(mainDoorLock.CurrentState, "Locked");

            // Enter the default combination of 000.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            IsEqual(mainDoorLock.CurrentState, "Locked");
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            IsEqual(mainDoorLock.CurrentState, "Locked");
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);

            // Should now be unlocked.
            IsEqual(mainDoorLock.CurrentState, "Unlocked");

            // Test the default handler. Should stay in the same state.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_5);
            IsEqual(mainDoorLock.CurrentState, "Unlocked");

            // Lock it again.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_Reset);
            IsEqual(mainDoorLock.CurrentState, "Locked");

            // Unlock it again.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_0);
            IsEqual(mainDoorLock.CurrentState, "Unlocked");

            // Must be in the unlocked state to change the combination.
            // Press set, new combo, set, set the combination to 123.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_Set);
            IsEqual(mainDoorLock.CurrentState, "SettingCombo");

            IsEqual(mainDoorLock.SM.ProcessEvent("NGEVENT"), false);

            IsEqual(mainDoorLock.CurrentState, "SettingCombo");

            // The state machine is now dead and will no longer process events.
            IsGreater(mainDoorLock.SM.Errors.Count, 0);

            mainDoorLock.PressKey(CombinationLock.Keys.Key_1);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_2);
            mainDoorLock.PressKey(CombinationLock.Keys.Key_3);
            IsEqual(mainDoorLock.CurrentState, "SettingCombo");

            mainDoorLock.PressKey(CombinationLock.Keys.Key_Set);

            IsEqual(mainDoorLock.CurrentState, "Unlocked");

            // Default state test.
            mainDoorLock.PressKey(CombinationLock.Keys.Key_Power);

            IsEqual(mainDoorLock.CurrentState, "Locked");

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

        void IsEqual<T>(T value1, T value2, [CallerFilePath] string file = "???", [CallerLineNumber] int line = -1) where T : IComparable
        {
            if (value1.CompareTo(value2) == 0)
            {
                Console.WriteLine($"[{value1}] should be [{value2}] : {file}({line})");
            }
        }
        void IsGreater<T>(T value1, T value2, [CallerFilePath] string file = "???", [CallerLineNumber] int line = -1) where T : IComparable
        {
            if (value1.CompareTo(value2) != 1)
            {
                Console.WriteLine($"[{value1}] should be greater than [{value2}] : {file}({line})");
            }
        }
    }
}