using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NBagOfTricks.Slog;


namespace NStateMachine.Demo
{
    /// <summary>
    /// Shows how to implement a state machine in an application.
    /// Also does rudimentary testing.
    /// </summary>
    public class DemoApp
    {
        Lock? _lock = null;

        readonly Logger _logger = LogManager.CreateLogger("DemoApp");

        public void Run()
        {
            // Set up logging.
            LogManager.MinLevelFile = Level.Trace;
            LogManager.MinLevelNotif = Level.Debug;
            LogManager.LogEvent += LogManager_LogEvent;
            LogManager.Run();

            // Create a new combo lock.
            _lock = new();

            var errors = _lock.Init();
            IsEqual(errors.Count, 0); // There is one syntax error.
            errors.ForEach(e => _logger.LogError(e));

            _lock.Run();

            // Should come up in the locked state.
            IsEqual(_lock.CurrentState, S.Locked);

            // Enter the default combination of 000.
            _lock.PressKey(Lock.Keys.Key_0);
            IsEqual(_lock.CurrentState, S.Locked);
            _lock.PressKey(Lock.Keys.Key_0);
            IsEqual(_lock.CurrentState, S.Locked);
            _lock.PressKey(Lock.Keys.Key_0);
            // Should now be unlocked.
            IsEqual(_lock.CurrentState, S.Unlocked);

            // Test the default handler. Should stay in the same state.
            _lock.PressKey(Lock.Keys.Key_5);
            IsEqual(_lock.CurrentState, S.Unlocked);

            // Lock it again.
            _lock.PressKey(Lock.Keys.Key_Reset);
            IsEqual(_lock.CurrentState, S.Locked);

            // Unlock it again.
            _lock.PressKey(Lock.Keys.Key_0);
            _lock.PressKey(Lock.Keys.Key_0);
            _lock.PressKey(Lock.Keys.Key_0);
            IsEqual(_lock.CurrentState, S.Unlocked);

            // Must be in the unlocked state to change the combination.
            // Press set, new combo, set, set the combination to 123.
            _lock.PressKey(Lock.Keys.Key_Set);
            IsEqual(_lock.CurrentState, S.SettingCombo);

            _lock.PressKey(Lock.Keys.Key_1);
            _lock.PressKey(Lock.Keys.Key_2);
            _lock.PressKey(Lock.Keys.Key_3);
            IsEqual(_lock.CurrentState, S.SettingCombo);

            _lock.PressKey(Lock.Keys.Key_Set);
            IsEqual(_lock.CurrentState, S.Unlocked);

            // Default state test.
            _lock.PressKey(Lock.Keys.Key_Reset);
            IsEqual(_lock.CurrentState, S.Locked);
            _lock.PressKey(Lock.Keys.Key_Power);
            IsEqual(_lock.CurrentState, S.Locked);

            // Make a picture.
            string sdot = _lock.GenerateDot("Lock Logic");
            File.WriteAllText("testout.gv", sdot);
            using Process p = new();
            p.StartInfo.FileName = "dot";
            p.StartInfo.Arguments = "-Tpng testout.gv -o ..\\..\\..\\testout.png";
            bool ok = p.Start();

            LogManager.Stop();
        }

        void LogManager_LogEvent(object? sender, LogEventArgs e)
        {
            Debug.WriteLine(e.Message);
            Console.WriteLine(e.Message);
        }

        void IsEqual<T>(T value1, T value2, [CallerFilePath] string file = "???", [CallerLineNumber] int line = -1) where T : IComparable
        {
            if (value1.CompareTo(value2) != 0)
            {
                string s = $"FAIL [{value1}] should be [{value2}] : {file}({line})";
                _logger.LogError(s);
            }
        }
    }
}