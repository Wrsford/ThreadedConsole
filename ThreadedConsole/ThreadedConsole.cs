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
        private class ConsoleState
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
        private static readonly Dictionary<int, ConcurrentQueue<ConsoleLogEntry>> _logQueue = new Dictionary<int, ConcurrentQueue<ConsoleLogEntry>>();
        /// <summary>
        /// Complex lock for safe modification of the log queue
        /// </summary>
        private static readonly ReaderWriterLock _logQueueDictLock = new ReaderWriterLock();

        /// <summary>
        /// ThreadID -> ConsoleState
        /// </summary>
        private static readonly Dictionary<int, ConsoleState> _logStates = new Dictionary<int, ConsoleState>();

        /// <summary>
        /// Complex lock for safe modification of the log states
        /// </summary>
        private static readonly ReaderWriterLock _logStatesDictLock = new ReaderWriterLock();

        /// <summary>
        /// Minimum time between flushes. Prevents flickering and helps readability. Actual debounce time is calculated as (_debounce * (logQueue.Count - 1) * 5)
        /// </summary>
        private static TimeSpan _debounce = TimeSpan.FromMilliseconds(20);

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
            while (true)
            {
                // Dynamically increase debounce time based on the number of active threads.
                if ((DateTime.Now - _lastOutput) > _debounce * ((_logQueue.Count - 1) * 5))
                {
                    Flush();
                    _lastOutput = DateTime.Now;
                }
                Thread.Sleep(_debounce);
            }
        }

        /// <summary>
        /// Writes all logs to the console
        /// </summary>
        public static void Flush()
        {
            _logQueueDictLock.AcquireReaderLock(Timeout.Infinite);

            // Track empty threads to remove for debounce timing accuracy
            List<int> keysToRemove = new List<int>();
            foreach (var logQueuePair in _logQueue.OrderByDescending(x => x.Key))
            {
                var logQueue = logQueuePair.Value;
                int threadId = logQueuePair.Key;
                
                if (logQueue.Count == 0)
                {
                    // Empties get removed later
                    keysToRemove.Add(threadId);
                    // No output needed
                    continue;
                }

                // Add a return after each thread to avoid overlapping logs (unless the log ends with a return)
                bool needsReturn = true;

                // Thanks for this while-loop copilot
                while (logQueue.TryDequeue(out ConsoleLogEntry logEntry))
                {

                    string log = logEntry.Log;
                    
                    // Restore the original colors after writing the log
                    var oldColor = Console.ForegroundColor;
                    var oldBgColor = Console.BackgroundColor;
                    
                    // Thread ID
                    if (ShowThreadIds)
                    {
                        // if the log starts with a carriage return, steal it for the thread id
                        var leadingReturn = log.StartsWith("\r") ? "\r" : "";
                        
                        // Choose a color based on the thread id
                        Console.ForegroundColor = (ConsoleColor)(threadId % 14 + 1);
                        Console.Write($"{leadingReturn}{threadId.ToString("D3")}: ");
                        // Trim the leading return if it was stolen
                        log = log.TrimStart('\r');
                    }

                    if (ShowTimestamps)
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
                    needsReturn = !log.EndsWith(Environment.NewLine);
                }

                // Add the return if needed
                if (needsReturn)
                {
                    Console.WriteLine();
                }
            }
            _logQueueDictLock.ReleaseReaderLock();

            // Remove empty threads if needed
            if (keysToRemove.Count > 0)
            {
                _logQueueDictLock.AcquireWriterLock(Timeout.Infinite);
                foreach (var key in keysToRemove)
                {
                    _logQueue.Remove(key);
                }
                _logQueueDictLock.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// Gets the console state for the calling thread
        /// </summary>
        /// <returns>Console state for the calling thread</returns>
        private static ConsoleState GetState()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            _logStatesDictLock.AcquireReaderLock(Timeout.Infinite);
            ConsoleState state;
            if (!_logStates.ContainsKey(threadId))
            {
                _logStatesDictLock.ReleaseReaderLock();
                _logStatesDictLock.AcquireWriterLock(Timeout.Infinite);
                _logStates.Add(threadId, new ConsoleState(ConsoleColor.Gray, ConsoleColor.Black));
                _logStatesDictLock.ReleaseWriterLock();
                _logStatesDictLock.AcquireReaderLock(Timeout.Infinite);
            }
            state = _logStates[threadId];
            _logStatesDictLock.ReleaseReaderLock();
            return state;
        }

        /// <summary>
        /// Sets the console state for the calling thread
        /// </summary>
        /// <param name="state">Console state for the calling thread</param>
        private static void SetState(ConsoleState state)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            _logStatesDictLock.AcquireWriterLock(Timeout.Infinite);
            _logStates[threadId] = state;
            _logStatesDictLock.ReleaseWriterLock();
        }


        /// <summary>
        /// Writes a log entry to the console without a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void Write(string log) => Write((object)log);

        /// <summary>
        /// Writes a log entry to the console without a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void Write(object? log)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            _logQueueDictLock.AcquireReaderLock(Timeout.Infinite);
            ConcurrentQueue<ConsoleLogEntry> logQueue;

            if (_logQueue.ContainsKey(threadId))
            {
                logQueue = _logQueue[threadId];
                _logQueueDictLock.ReleaseReaderLock();
            }
            else
            {
                _logQueueDictLock.ReleaseReaderLock();
                _logQueueDictLock.AcquireWriterLock(Timeout.Infinite);
                _logQueue[threadId] = new ConcurrentQueue<ConsoleLogEntry>();
                logQueue = _logQueue[threadId];
                _logQueueDictLock.ReleaseWriterLock();
            }
            var state = GetState();
            var logEntry = new ConsoleLogEntry(log == null ? "" : log!.ToString(), state.ForegroundColor, state.BackgroundColor);
            logQueue.Enqueue(logEntry);
        }

        /// <summary>
        /// Writes a log entry to the console with a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void WriteLine(string log) => WriteLine((object)log);

        /// <summary>
        /// Writes a log entry to the console with a trailing newline
        /// </summary>
        /// <param name="log">Text to write</param>
        public static void WriteLine(object? log)
        {
            Write((log ?? "") + Environment.NewLine);
        }
    }
}
