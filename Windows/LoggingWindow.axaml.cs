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

        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<string> FilteredLogs { get; } = new();

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
                            Logs.Add(formattedLog);
                            if (Logs.Count > 1000)
                            {
                                Logs.RemoveAt(0);
                            }
                            
                            if (ScrollToBottom)
                            {
                                ScrollToEnd();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ERROR] Log parsing failed: " + ex.Message);
                        Dispatcher.UIThread.Post(() => Logs.Add(message));
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
            FilteredLogs.Clear();
            foreach (var log in Logs.Where(log => log.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredLogs.Add(log);
            }
        }

        private void ScrollToEnd()
        {
            if (FilteredLogs.Count > 0)
            {
                var listBox = this.FindControl<ListBox>("LogListBox");
                listBox.ScrollIntoView(FilteredLogs.Last());
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
