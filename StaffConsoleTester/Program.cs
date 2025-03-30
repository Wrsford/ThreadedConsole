using StaffConsole;

// See https://aka.ms/new-console-template for more information




for (int i = 0; i < 2; i++)
{
    break;
    var z = i;
    Task.Run(() =>
    {
        var myProgressBar = new ConsoleProgressBar($"Thread {z}");
        while (true)
        {
            for (int x = 0; x < 100; x++)
            {
                ThreadedConsole.Write(myProgressBar.LogProgress((float)x / 100));
                Thread.Sleep(Random.Shared.Next(500));
            }
        }
            
    });
}
ThreadedConsole.ShowThreadIds = true;
ThreadedConsole.ShowTimestamps = true;
while (true)
{
    //ThreadedConsole.WriteLine($"{Guid.NewGuid()}");
    Thread.Sleep(500);
    if (Random.Shared.Next(10) == 0)
    {

        var z = Random.Shared.Next(50);
        Task.Run(() =>
        {
            var myProgressBar = new ConsoleProgressBar($"Thread {z}");
            var myColor = (ConsoleColor)(Random.Shared.Next(14) + 1);
            for (int x = 0; x < 100; x++)
            {
                int y = x;
                Task.Run(() =>
                {
                    var oldColor = ThreadedConsole.ForegroundColor;
                    ThreadedConsole.ForegroundColor = myColor;
                    ThreadedConsole.Write(myProgressBar.LogProgress((float)y / 100));
                    ThreadedConsole.ForegroundColor = oldColor;
                    
                });
                Thread.Sleep(Random.Shared.Next(20));

            }

        });
    }
}