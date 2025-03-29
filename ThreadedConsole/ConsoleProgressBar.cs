// Copyright 2025 Will Stafford. All rights reserved.

namespace StaffConsole
{
    public class ConsoleProgressBar
    {
        private DateTime LastUpdate = DateTime.MinValue;
        private float Progress = 0.0f;
        private string Lable { get; set; } = "";
        public ConsoleProgressBar(string lable)
        {
            Lable = lable;
        }

        public string LogProgress(float percent)
        {
            var sinceLastUpdate = DateTime.Now.Subtract(LastUpdate).TotalMilliseconds;
            var newDLProgress = percent * 100.0f;
            Progress = newDLProgress;

            float fullPer = percent * 100;
            LastUpdate = DateTime.Now;
            // Make bar
            string bar = "[";
            for (int i = 1; i <= 95; i += 5)
            {
                if (i <= fullPer)
                {
                    bar += "=";
                }
                else
                {
                    bar += " ";
                }
            }
            bar += $"] {fullPer.ToString("0.0")}%";
            if (percent == 1.0)
            {
                return $"\r{bar} | {Lable}" + Environment.NewLine;
            }
            else
            {
                return $"\r{bar} | {Lable}";
            }
        }
    }
}
