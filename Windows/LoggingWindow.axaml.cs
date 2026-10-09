using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace UnrealFrontiersLauncher
{
    public partial class LoggingWindow : Window, INotifyPropertyChanged
    {
        private ConcurrentQueue<string> _logQueue = new();
        private StringBuilder _logBuffer = new();
        private bool _isRunning = true;

        private ObservableCollection<string> _logs = new();
        private ObservableCollection<string> _filteredLogs = new();
        public ObservableCollection<string> FilteredLogs
        {
            get => _logs;
        }

        private string _searchQuery = "";
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                ApplyFilter();
            }
        }

        private bool _scrollToBottom = true;
        public bool ScrollToBottom
        {
            get => _scrollToBottom;
            set
            {
                _scrollToBottom = value;
                OnPropertyChanged(nameof(ScrollToBottom));
            }
        }
        public LoggingWindow()
        {
            InitializeComponent();
            DataContext = this;

            Thread logThread = new(ProcessLogs)
            {
                IsBackground = true
            };
            logThread.Start();
        }

        public LoggingWindow(string gameName) : this()
        {
            Title = $"{gameName} - Console Output";
        }

        public void AppendLog(string message)
        {
            _logQueue.Enqueue(message);
        }

        private void ProcessLogs()
        {
            while (_isRunning)
            {
                if (_logQueue.TryDequeue(out string? message))
                {
                    _logBuffer.Append(message);

                    if (!message.Trim().EndsWith("</log4j:Event>"))
                    {
                        continue;
                    }

                    string completeXml = _logBuffer.ToString();
                    _logBuffer.Clear();

                    try
                    {
                        string cleanedXml = completeXml.Replace("log4j:", "");
                        XDocument doc = XDocument.Parse(cleanedXml);

                        XElement logEvent = doc.Descendants("Event").FirstOrDefault();
                        if (logEvent == null) continue;

                        string timestamp = logEvent.Attribute("timestamp")?.Value ?? "Unknown";
                        string level = logEvent.Attribute("level")?.Value ?? "Unknown";
                        string msg = logEvent.Element("Message")?.Value ?? "No message";

                        long millis = long.TryParse(timestamp, out long t) ? t : 0;
                        DateTime time = DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
                        string formattedTime = time.ToString("HH:mm:ss");

                        string formattedLog = $"[{formattedTime}] [{level}] {msg}";

                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _logs.Add(formattedLog);
                            
                            if (_logs.Count > 1000)
                            {
                                _logs.RemoveAt(0);
                            }

                            if (ScrollToBottom)
                            {
                                // ScrollToEnd();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ERROR] Log parsing failed: " + ex.Message);
                        Dispatcher.UIThread.Post(() => _logs.Add(message));
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrEmpty(_searchQuery) 
                ? _logs 
                : _logs.Where(log => log.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));
            
        }

        private void ScrollToEnd()
        {
            if (FilteredLogs.Count > 0)
            {
                LogsListBox.ScrollIntoView(FilteredLogs.Last());
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isRunning = false;
            base.OnClosed(e);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
