using System;
using System.Text;
using System.Windows.Media;

namespace SIinformer.Utils
{
    public class Logger : BindableObject
    {
        private readonly StringBuilder _errorLog = new StringBuilder();
        private readonly StringBuilder _log = new StringBuilder();
        private bool _isError;
        private string _message = "";
        private bool _working;

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
            if (s.StartsWith("Google:"))
                s = s.Replace("Google:", "Google " + DateTime.Now.ToShortTimeString() + " - ");
            _log.Insert(0, s + Environment.NewLine);
            if (isError)
            {
                _errorLog.Insert(0, s + Environment.NewLine);
                IsError = true;
            }
            if (toMessage) Message = s;
            RaisePropertyChanged("Log");
        }
    }
}