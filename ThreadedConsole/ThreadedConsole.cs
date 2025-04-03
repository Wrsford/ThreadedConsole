// Copyright 2025 Will Stafford. All rights reserved.

using System.Collections.Concurrent;

namespace StaffConsole
{

    /// <summary>
    /// Console for multi-threaded applications that need to write to the console from multiple threads. Automatically debounces writes to the console to prevent flickering.
    /// </summary>
    public static class ThreadedConsole
    {
        /// <summary>
        /// Per-thread conosle state
        /// </summary>
        public class ConsoleState
        {
            public ConsoleColor ForegroundColor { get; set; }
            public ConsoleColor BackgroundColor { get; set; }

            public ConsoleState(ConsoleColor fg, ConsoleColor bg)
            {
                ForegroundColor = fg;
                BackgroundColor = bg;
            }

        }

        /// <summary>
        /// ThreadID -> LogQueue
        /// </summary>
        private static readonly ConcurrentQueue<ConsoleLogEntry> _logQueue = new ConcurrentQueue<ConsoleLogEntry>();

        /// <summary>
        /// ThreadID -> ConsoleState
        /// </summary>
        private static readonly ConcurrentDictionary<int, ConsoleState> _logStates = new ConcurrentDictionary<int, ConsoleState>();

        /// <summary>
        /// Minimum time between flushes. Prevents flickering and helps readability. Actual debounce time is calculated as (_debounce * (logQueue.Count - 1) * 5)
        /// </summary>
        private static TimeSpan _debounce = TimeSpan.FromMilliseconds(40);

        private static int _maximumLogDequeueSize = 10000;

        private static bool _debugSlowMode = false;

        /// <summary>
        /// Last time the ThreadedConsole was flushed; used for debounce.
        /// </summary>
        private static DateTime _lastOutput = DateTime.MinValue;


        /// <summary>
        /// The foreground color of the console for the calling thread
        /// </summary>
        public static ConsoleColor ForegroundColor
        {
            get
            {
                return GetState().ForegroundColor;
            }
            set
            {
                var state = GetState();
                state.ForegroundColor = value;
                SetState(state);
            }
        }

        /// <summary>
        /// The background color of the console for the calling thread
        /// </summary>
        public static ConsoleColor BackgroundColor
        {
            get
            {
                return GetState().BackgroundColor;
            }
            set
            {
                var state = GetState();
                state.BackgroundColor = value;
                SetState(state);
            }
        }

        /// <summary>
        /// Outputs a 3 digit thread id for the calling thread before each log entry
        /// </summary>
        public static bool ShowThreadIds { get; set; } = false;

        /// <summary>
        /// Outputs a timestamp before each log entry
        /// </summary>
        public static bool ShowTimestamps { get; set; } = false;

        public static bool DisableOutput { get; set; } = false;

        private static int _lastOutputtedThread = -1;

        static ThreadedConsole()
        {
            // Start the flush loop
            Task.Run(() =>
            {
                FlushLoop();
            });
        }

        /// <summary>
        /// The loop that flushes the logs to the console with a debounce
        /// </summary>
        private static void FlushLoop()
        {
            int origDeque = _maximumLogDequeueSize;
            while (true)
            {
                // Dynamically increase debounce time based on the number of active threads.
                var adjust = _logQueue.Count / (_maximumLogDequeueSize / 100.0) * _debounce;
                
                if ((DateTime.Now - _lastOutput) > _debounce + adjust)
                {
                    _maximumLogDequeueSize = _logQueue.Count;
                    Flush();
                    _maximumLogDequeueSize = origDeque;
                    _lastOutput = DateTime.Now;
                }
                Thread.Sleep(_debounce / 2);
            }
        }

        /// <summary>
        /// Writes all logs to the console
        /// </summary>
        public static void Flush()
        {
            if (DisableOutput)
            {
                return;
            }


            int maximum = _maximumLogDequeueSize;
            Dictionary<int, ConcurrentQueue<ConsoleLogEntry>> logs = new Dictionary<int, ConcurrentQueue<ConsoleLogEntry>>();
            while (_logQueue.TryDequeue(out ConsoleLogEntry? logEntry) && maximum-- > 0)
            {
                if (!logs.ContainsKey(logEntry.ThreadId))
                {
                    logs[logEntry.ThreadId] = new ConcurrentQueue<ConsoleLogEntry>();
                }
                logs[logEntry.ThreadId].Enqueue(logEntry);
            }

            foreach (var logQueuePair in logs.OrderByDescending(x => x.Key))
            {
                var logQueue = logQueuePair.Value;
                int threadId = logQueuePair.Key;

                if (logQueue.Count == 0)
                {
                    // Empties get removed later
                    //keysToRemove.Add(threadId);
                    // No output needed
                    continue;
                }

                // Add a return after each thread to avoid overlapping logs (unless the log ends with a return)
                bool needsReturn = true;
                bool isAtStart = true;
                // Thanks for this while-loop copilot
                while (logQueue.TryDequeue(out ConsoleLogEntry logEntry))
                {
                    if (_debugSlowMode)
                    {
                        // Slow down the output for debugging purposes
                        // This is useful for seeing the output, but slows down the program significantly
                        // DO NOT USE IN PROD
                        Thread.Sleep(50);
                    }

                    string log = logEntry.Log;
                    isAtStart = Console.CursorLeft == 0 || log.StartsWith("\r");
                    // Restore the original colors after writing the log
                    var oldColor = Console.ForegroundColor;
                    var oldBgColor = Console.BackgroundColor;

                    // Thread ID
                    if (ShowThreadIds && isAtStart)
                    {
                        // if the log starts with a carriage return, steal it for the thread id
                        var leadingReturn = log.StartsWith("\r") ? "\r" : "";

                        // Choose a color based on the thread id
                        Console.ForegroundColor = (ConsoleColor)(threadId % 14 + 1);
                        Console.Write($"{leadingReturn}{threadId.ToString("D3")}: ");
                        // Trim the leading return if it was stolen
                        log = log.TrimStart('\r');
                    }

                    if (ShowTimestamps && isAtStart)
                    {
                        // if the log starts with a carriage return, steal it for the timestamp
                        var leadingReturn = log.StartsWith("\r") ? "\r" : "";

                        // I like dark gray for timestamps
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"{leadingReturn}[{logEntry.LogTime.ToString("T")}] ");

                        // Trim the leading return if it was stolen
                        log = log.TrimStart('\r');
                    }

                    // Write the log with the appropriate colors
                    Console.ForegroundColor = logEntry.ForegroundColor;
                    Console.BackgroundColor = logEntry.BackgroundColor;
                    Console.Write(log);

                    // Restore the original colors
                    Console.ForegroundColor = oldColor;
                    Console.BackgroundColor = oldBgColor;

                    // Check if the log ends with a return
                    needsReturn = !log.EndsWith(Environment.NewLine) && _logQueue.Count > 1;
                    _lastOutputtedThread = logEntry.ThreadId;
                }

                // Add the return if needed
                if (needsReturn)
                {
                    Console.WriteLine();
                }
            }


        }

        /// <summary>
        /// Gets the console state for the calling thread
        /// </summary>
        /// <returns>Console state for the calling thread</returns>
        public static ConsoleState GetState(int? forcedThreadId = null)
        {
            if (DisableOutput)
            {
                return new ConsoleState(ConsoleColor.White, ConsoleColor.Black);
            }
            int threadId = forcedThreadId ?? Thread.CurrentThread.ManagedThreadId;
            ConsoleState state = _logStates.GetOrAdd(threadId, new ConsoleState(ConsoleColor.Gray, ConsoleColor.Black));
            return state;
        }

        /// <summary>
        /// Sets the console state for the calling thread
        /// </summary>
        /// <param name="state">Console state for the calling thread</param>
        public static void SetState(ConsoleState state, int? forcedThreadId = null)
        {
            if (DisableOutput)
            {
                return;
            }
            int threadId = forcedThreadId ?? Thread.CurrentThread.ManagedThreadId;
            _logStates[threadId] = state;
        }


        /// <summary>
        /// Writes a log entry to the console without a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void Write(string log, int? forcedThreadId = null) => Write((object)log, forcedThreadId);

        /// <summary>
        /// Writes a log entry to the console without a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void Write(object? log, int? forcedThreadId = null)
        {
            if (DisableOutput)
            {
                return;
            }
            int threadId = forcedThreadId ?? Thread.CurrentThread.ManagedThreadId;
            ConcurrentQueue<ConsoleLogEntry> logQueue = _logQueue;

            var state = GetState();
            var logEntry = new ConsoleLogEntry(threadId, log == null ? "" : log!.ToString(), state.ForegroundColor, state.BackgroundColor);
            logQueue.Enqueue(logEntry);
        }

        /// <summary>
        /// Writes a log entry to the console with a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void WriteLine(string log, int? forcedThreadId = null) => WriteLine((object)log, forcedThreadId);

        /// <summary>
        /// Writes a log entry to the console with a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void WriteLine(object? log, int? forcedThreadId = null)
        {
            Write((log ?? "") + Environment.NewLine, forcedThreadId);
        }
    }
}
