using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StaffConsole
{
    public class ConsoleLogEntry
    {
        public int ThreadId { get; private set; }
        public string Log { get; private set; }
        public ConsoleColor ForegroundColor { get; private set; }
        public ConsoleColor BackgroundColor { get; private set; }
        public DateTime LogTime { get; private set; }

        

        public ConsoleLogEntry(int threadId, string log, ConsoleColor fgColor, ConsoleColor bgColor)
        {
            ThreadId = threadId;
            Log = log;
            ForegroundColor = fgColor;
            BackgroundColor = bgColor;
            LogTime = DateTime.Now;
        }
    }
}
