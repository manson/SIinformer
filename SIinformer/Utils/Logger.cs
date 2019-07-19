using System;
using System.Collections.Concurrent;
using System.Text;
using System.Timers;
using System.Windows.Media;

namespace SIinformer.Utils
{
    public class Logger : BindableObject
    {
        private readonly StringBuilder _errorLog = new StringBuilder();
        private readonly StringBuilder _log = new StringBuilder();
        private readonly ConcurrentQueue<LogItem> _logBuffer = new ConcurrentQueue<LogItem>();
        private bool _isError;
        private string _message = "";
        private bool _working;
        private Timer _logTimer;

        public Logger()
        {
            _logTimer = new Timer(100);
            _logTimer.Elapsed += (s, e) => LogQueueProcess();
            _logTimer.AutoReset = false;
            _logTimer.Start();
        }

        public Brush Foreground
        {
            get { return IsError ? Brushes.Tomato : Brushes.White; }
        }

        public bool IsError
        {
            get { return _isError; }
            set
            {
                if (value != _isError)
                {
                    _isError = value;
                    RaisePropertyChanged("Log");
                    RaisePropertyChanged("Foreground");
                    RaisePropertyChanged("IsError");
                }
            }
        }

        public string Message
        {
            get { return _message; }
            private set
            {
                if (value != _message)
                {
                    _message = value;
                    RaisePropertyChanged("Message");
                }
            }
        }

        public bool Working
        {
            get { return _working; }
            set
            {
                if (value != _working)
                {
                    _working = value;
                    RaisePropertyChanged("Working");
                }
            }
        }

        public StringBuilder Log
        {
            get { return IsError ? _errorLog : _log; }
        }

        public void Add(string s)
        {
            Add(s, true, false);
        }

        public void Add(string s, bool toMessage)
        {
            Add(s, toMessage, false);
        }

        public void Add(string s, bool toMessage, bool isError)
        {
            _logBuffer.Enqueue(new LogItem{LogMessage = s, ToMessage = toMessage, IsError = isError});
            _logTimer.Start();
        }

        private void LogQueueProcess()
        {
            if (_logBuffer.IsEmpty) return;           
            LogItem logItem = null;
            while (_logBuffer.TryDequeue(out logItem))
            {
                if (!logItem.LogMessage.StartsWith("->"))
                {
                    _log.Insert(0, logItem.LogMessage + Environment.NewLine);
                    if (logItem.IsError)
                    {
                        _errorLog.Insert(0, logItem.LogMessage + Environment.NewLine);
                        IsError = true;
                    }
                }
                if (logItem.ToMessage) Message = logItem.LogMessage.StartsWith("->") ? logItem.LogMessage.Substring(2) : logItem.LogMessage;
                RaisePropertyChanged("Log");
            }
            _logTimer.Start(); // на всякий случай, повторно пройдемся. Если лог пуст, то на первой строчке этой функции все затухнет
        }

        private class LogItem
        {
            public string LogMessage { get; set; }
            public bool ToMessage { get; set; }
            public bool IsError { get; set; }
        }
    }
}