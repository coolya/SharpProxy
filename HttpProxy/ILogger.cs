using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace HttpProxy
{
    public interface ILogger
    {
        void Error(string msg);
        void Error(string msg, Exception ex);
        void Error(string msg, params object[] param);
        void Error(string msg, Exception ex, params object[] param);
        void Error(Exception ex);

        void Fatal(string msg);
        void Fatal(string msg, Exception ex);
        void Fatal(string msg, params object[] param);
        void Fatal(string msg, Exception ex, params object[] param);
        void Fatal(Exception ex);

        void Info(string msg);
        void Info(string msg, params object[] param);
         
        void Verbose(string msg);
        void Verbose(string msg, params object[] param);

        void Warning(string msg);
        void Warning(string msg, Exception ex);
        void Warning(string msg, params object[] param);
        void Warning(string msg, Exception ex, params object[] param);
        void Warning(Exception ex);
    }
}
