using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace MTM101BaldAPI.ErrorHandler
{
    public class LoadingErrorListener : ILogListener
    {
        public ModLoadingScreenManager manager;

        public void Dispose() { }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if (eventArgs.Level == (LogLevel.Fatal | LogLevel.Error))
            {
                manager.ThrowError(new Exception(eventArgs.Data.ToString()));
            }
        }
    }
}
