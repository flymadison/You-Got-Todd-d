using System.Diagnostics;
using System.Management;

class Program
{
    private static ManagementEventWatcher watcher;
    private static ManualResetEvent shutdownEvent = new ManualResetEvent(false);
    private static string todds = new($"0"); // Initialize the Todd count
    private static string filePath = ""; // File to store Todd count
    private static string game = "";
    static void Main(string[] args)
    {
        try
        {
            filePath = args[0];
            game = args[1];
        } catch (IndexOutOfRangeException)
        {
            Console.WriteLine("No todd file and/or game provided.");
            Environment.Exit(1);
        }

        if (!File.Exists(filePath))
        {
            File.Create(filePath).Close();
            File.WriteAllText(filePath, $"0"); // Initialize the file with 0
        }
        todds = File.ReadAllText(filePath);
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("\nStopping monitoring...");
            watcher.Stop();
            shutdownEvent.Set();
            eventArgs.Cancel = true; // Prevent abrupt process exit
        };
        
        Thread keyListener = new Thread(() =>
        {
            while (true) // Check for shutdown event
            {
                var keyInfo = Console.ReadKey(intercept: true);
                switch (keyInfo.Modifiers)
                {
                    case ConsoleModifiers.Control when keyInfo.Key == ConsoleKey.M:
                        todds = (int.Parse(todds) + 1).ToString(); // Increment Todd count
                        File.WriteAllText(filePath, (todds).ToString());
                        Console.WriteLine($"Current Todd count: {todds} Todds.");
                        break;
                    case ConsoleModifiers.Control when keyInfo.Key == ConsoleKey.N:
                        if (int.Parse(todds) > 0)
                        {
                            todds = (int.Parse(todds) - 1).ToString(); // Decrement Todd count
                            File.WriteAllText(filePath, todds);
                            Console.WriteLine($"Current Todd count: {todds} Todds.");
                        }
                        else
                        {
                            Console.WriteLine("Todd count cannot be negative.");
                        }
                        break;
                }
            }
        });

        keyListener.Start();

        StartWatcher();
        Console.WriteLine($"Monitoring for {game}.exe...\n\nPress Ctrl+C to exit\nPress Ctrl+M to +1 Todd count\nPress Ctrl+N to -1 Todd count\nCurrent Todd count: {todds} Todds.");
        shutdownEvent.WaitOne(); // Keep the main thread alive until Ctrl+C
    }

    static void StartWatcher()
    {
        var scope = new ManagementScope(@"\\.\root\CIMV2");
        scope.Connect();

        var queryString = $@"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA ""Win32_Process"" AND TargetInstance.Name = '{game}.exe'";
        var query = new WqlEventQuery(
            //$@"SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA ""Win32_Process"" AND TargetInstance.Name = 'SkyrimSE.exe'"
            queryString
        );

        watcher = new ManagementEventWatcher(scope, query);
        watcher.EventArrived += new EventArrivedEventHandler(OnSkyrimStarted);
        watcher.Start();
    }

    static void OnSkyrimStarted(object sender, EventArrivedEventArgs e)
    {
        var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
        int processId = Convert.ToInt32(targetInstance["ProcessId"]);

        Console.WriteLine($"{game}.exe started with PID {processId}");

        try
        {
            var process = Process.GetProcessById(processId);
            process.EnableRaisingEvents = true;

            process.Exited += (s, ev) =>
            {
                try
                {
                    int exitCode = process.ExitCode;
                    Console.WriteLine($"{game}.exe exited. Exit code: {exitCode}");

                    if (exitCode != 0 && exitCode != -1)
                    {
                        Console.WriteLine("Abnormal termination detected.\nIncrementing Todd count by 1.");
                        todds = (int.Parse(todds) + 1).ToString(); // Increment Todd count;
                        File.WriteAllText(filePath, (todds).ToString());
                    }
                    else
                    {
                        Console.WriteLine("Exited normally.");
                    }
                }

                catch
                {
                    Console.WriteLine($"{game}.exe exited, but exit code could not be read.");
                }
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not monitor process: {ex.Message}");
        }
    }
}
