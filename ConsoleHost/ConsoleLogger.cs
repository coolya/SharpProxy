using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HttpProxy;

namespace ConsoleHost
{
    [Flags]
    enum LoggerConfig
    {
        None = 0,
        Fatal = 1,
        Error = 2,
        Warning = 4,
        Info = 8,
        Verbose = 16,
        AllButVerbose = 15,
        WarningAndHigher = 7,
        ErrorAndFatalOnly = 3,
        All = 31
    }

    class ConsoleLogger : ILogger
    {
        private LoggerConfig _config; 

        public ConsoleLogger()
        {
            _config = LoggerConfig.All;
        }

        public ConsoleLogger(LoggerConfig config)
        {
            _config = config;
        }

        public void Error(string msg)
        {
            Error(msg, null, null);
        }

        public void Error(string msg, Exception ex)
        {
            Error(msg, null, ex);            
        }

        public void Error(Exception ex)
        {
            Error(string.Empty, ex, null);
        }

        public void Error(string msg, params object[] param)
        {
            Error(msg, null, param);
        }

        public void Error(string msg, Exception ex, params object[] param)
        {
            if ((_config & LoggerConfig.Error) == 0)
                return;

            Console.WriteLine(string.Format("ERROR [{0}]: {1} <{2}>", DateTime.Now, string.Format(msg, param ?? new object[]{}), ex));
        }

        public void Fatal(string msg)
        {
            Fatal(msg, null , null);
        }

        public void Fatal(string msg, Exception ex)
        {
            Fatal(msg, ex, null);
        }

        public void Fatal(Exception ex)
        {
            Fatal(string.Empty, ex, null);
        }

        public void Fatal(string msg, params object[] param)
        {
            Fatal(msg, null, param);
        }

        public void Fatal(string msg, Exception ex, params object[] param)
        {
            if ((_config & LoggerConfig.Fatal) == 0)
                return;

            Console.WriteLine(string.Format("FATAL [{0}]: {1} <{2}>", DateTime.Now, string.Format(msg, param ?? new object[] { }), ex));
        }

        public void Info(string msg)
        {
            Info(msg, null);
        }

        public void Info(string msg, params object[] param)
        {
            if ((_config & LoggerConfig.Info) == 0)
                return;

            Console.WriteLine(string.Format("INFO [{0}]: {1} ", DateTime.Now, string.Format(msg, param ?? new object[] { })));
        }

        public void Verbose(string msg)
        {
            Verbose(msg, null);
        }

        public void Verbose(string msg, params object[] param)
        {
            if ((_config & LoggerConfig.Verbose) == 0)
                return;

            Console.WriteLine(string.Format("VERBOSE [{0}]: {1} ", DateTime.Now, string.Format(msg, param ?? new object[] { })));
        }

        public void Warning(string msg)
        {
            Warning(msg, null, null);
        }

        public void Warning(string msg, Exception ex)
        {
            Warning(msg, ex, null);
        }

        public void Warning(Exception ex)
        {
            Warning(string.Empty, ex);
        }

        public void Warning(string msg, params object[] param)
        {
            Warning(msg, null, param);
        }

        public void Warning(string msg, Exception ex, params object[] param)
        {
            if ((_config & LoggerConfig.Warning) == 0)
                return;

            Console.WriteLine(string.Format("WARNING [{0}]: {1} <{2}>", DateTime.Now, string.Format(msg, param ?? new object[] { }), ex));
        }
    }
}
