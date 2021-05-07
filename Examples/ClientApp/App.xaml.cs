using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ClientApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            ConfigureLogger();
        }

        static void ConfigureLogger()
        {
            var config = new LoggingConfiguration();
            var debuggerTarget = new DebuggerTarget("debugger")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };
            config.AddTarget(debuggerTarget);
            config.AddRuleForAllLevels(debuggerTarget);
            LogManager.Configuration = config;
        }
    }
}
