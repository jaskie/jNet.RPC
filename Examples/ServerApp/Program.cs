using jNet.RPC.Server;
using NLog;
using NLog.Config;
using NLog.Targets;
using ServerApp.Model;
using System;

namespace ServerApp
{
    class Program
    {
        const int ListenPort = 1356;

        static void ConfigureLogger()
        {
            var config = new LoggingConfiguration();
            var consoleTarget = new ColoredConsoleTarget("console")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };
            config.AddTarget(consoleTarget);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);
            LogManager.Configuration = config;
        }

        static void Main(string[] args)
        {
            Console.Title = $"Server host on port {ListenPort}";
            ConfigureLogger();
            try
            {
                using (var host = new ServerHost(ListenPort, new RootElement()))
                {
                    bool terminate = false;
                    while (!terminate)
                    {
                        Console.Write('>');
                        var line = Console.ReadLine();
                        var lineParts = line?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lineParts?.Length >= 1)
                            switch (lineParts[0].ToLower())
                            {
                                case "quit":
                                    var clientCount = host.ClientCount;
                                    if (clientCount > 0)
                                    {
                                        Console.WriteLine($"There {(clientCount == 1 ? "is" : "are")} {clientCount} connected client{(clientCount == 1 ? "" : "s")}.\nUse forcequit command to terminate application.");
                                        continue;
                                    }
                                    terminate = true;
                                    break;
                                case "forcequit":
                                    terminate = true;
                                    break;
                                default:
                                    Console.WriteLine(@"Available commands:
- quit
- forcequit");
                                    break;

                            }
                    }
                }
            }
            catch
            {
                Console.WriteLine("Unable to start server host");
                Console.ReadKey();
                return;
            }
        }
    }
}
