using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net.Appender;
using log4net.Core;
using log4net.Util;

namespace Log4Slack
{
    public class SlackAppender : AppenderSkeleton
    {
        private readonly ManualResetEventSlim _resetEvent = new ManualResetEventSlim(true);

        private int _count;
        private object _lock = new object();

        private readonly Process _currentProcess = Process.GetCurrentProcess();

        private readonly Dictionary<string, Mapping> _mappings =
            new Dictionary<string, Mapping>(StringComparer.InvariantCultureIgnoreCase);

        private SlackClient? _slackClient;

        public bool AddAttachment { get; set; }

        public bool AddExceptionTraceField { get; set; }

        public string Channel { get; set; }

        public string IconEmoji { get; set; }

        public string IconUrl { get; set; }

        public bool LinkNames { get; set; }

        public Mapping Mapping
        {
            set => _mappings.Add(value.Level, value);
        }

        public string Proxy { get; set; }

        public string Username { get; set; }

        public bool UsernameAppendLoggerName { get; set; }

        public string WebhookUrl { get; set; }

        public override void ActivateOptions()
        {
            base.ActivateOptions();
            _slackClient = new SlackClient(WebhookUrl.Expand(), Username, Channel, IconUrl, IconEmoji);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            List<Attachment> list = new List<Attachment>();
            if (AddAttachment)
            {
                Attachment attachment = CreateAttachment(loggingEvent);
                list.Add(attachment);
            }

            string text2 = Layout != null ? Layout.FormatString(loggingEvent) : loggingEvent.RenderedMessage;
            string username = Username.Expand() + (UsernameAppendLoggerName ? " - " + loggingEvent.LoggerName : null);

            if (_slackClient == null)
            {
                LogLog.Error(typeof(SlackAppender), "Slack appender was not activated");
            }

            Task postTask = _slackClient!.PostMessageAsync(
                text2,
                Proxy,
                username,
                Channel.Expand(),
                IconUrl.Expand(),
                IconEmoji.Expand(),
                list,
                LinkNames);

            WatchTask(postTask);
        }

        protected override void OnClose()
        {
            LogLog.Debug(typeof(SlackAppender), "Waiting for all tasks to complete");
            _resetEvent.Wait();
            LogLog.Debug(typeof(SlackAppender), "All tasks completed");
            _slackClient?.Dispose();
            base.OnClose();
            LogLog.Debug(typeof(SlackAppender), "Slack appender closed");
        }

        private Attachment CreateAttachment(LoggingEvent loggingEvent)
        {
            lock (_lock)
            {
                _count++;
                LogLog.Debug(typeof(SlackAppender), $"Watched task count: {_count}");
                _resetEvent.Reset();
            }

            try
            {
                Attachment attachment = new Attachment(
                    $"[{loggingEvent.Level.DisplayName}] {loggingEvent.LoggerName} in {_currentProcess.ProcessName} on {Environment.MachineName}")
                {
                    Color = loggingEvent.Level.DisplayName.ToLowerInvariant() switch
                    {
                        "warn" => "warning",
                        "error" or "fatal" => "danger",
                        _ => string.Empty,
                    },
                    Fields =
                    {
                        new Field("Process", _currentProcess.ProcessName, true),
                        new Field("Machine", Environment.MachineName, true),
                    },
                };

                if (_mappings.TryGetValue(loggingEvent.Level.DisplayName, out Mapping mapping))
                {
                    Color color = Color.FromName(mapping.BackColor);
                    string text = color.IsNamedColor ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : mapping.BackColor;
                    attachment = attachment with
                    {
                        Color = text,
                    };
                }

                if (!UsernameAppendLoggerName)
                {
                    attachment.Fields.Insert(0, new Field("Logger", loggingEvent.LoggerName, true));
                }

                Exception exceptionObject = loggingEvent.ExceptionObject;
                if (exceptionObject != null)
                {
                    FormatException(attachment, exceptionObject);
                }

                return attachment;
            }
            finally
            {
                lock (_lock)
                {
                    --_count;
                    LogLog.Debug(typeof(SlackAppender), $"Watched task count: {_count}");
                    if (_count == 0)
                    {
                        LogLog.Debug(typeof(SlackAppender), "Watched task count is zero, setting reset event");
                        _resetEvent.Set();
                    }
                }
            }
        }

        private void FormatException(Attachment attachment, Exception exceptionObject)
        {
            attachment.Fields.Insert(0, new Field("Exception Type", exceptionObject.GetType().Name, true));
            if (AddExceptionTraceField && !string.IsNullOrWhiteSpace(exceptionObject.StackTrace))
            {
                string[] array = exceptionObject.StackTrace.SplitOn(1990).ToArray();
                for (int num = array.Length - 1; num >= 0; num--)
                {
                    string title = "Exception Trace" + (num > 0 ? $" {num + 1}" : null);
                    attachment.Fields.Insert(
                        0,
                        new Field(title, "```" + array[num].Replace("```", "'''") + "```"));
                }
            }

            attachment.Fields.Insert(0, new Field("Exception Message", exceptionObject.Message));
        }

        private void WatchTask(Task task)
        {
            lock (_lock)
            {
                _count++;
                LogLog.Debug(typeof(SlackAppender), $"Watched task count: {_count}");
                _resetEvent.Reset();
            }

            task.ContinueWith(
                t =>
                {
                    LogLog.Debug(typeof(SlackAppender), "Watched task completed");
                    if (t.IsFaulted)
                    {
                        ErrorHandler.Error("Error sending message to Slack", t.Exception);
                    }

                    lock (_lock)
                    {
                        --_count;
                        LogLog.Debug(typeof(SlackAppender), $"Watched task count: {_count}");
                        if (_count == 0)
                        {
                            LogLog.Debug(typeof(SlackAppender), "Watched task count is zero, setting reset event");
                            _resetEvent.Set();
                        }
                    }
                });
        }
    }
}
