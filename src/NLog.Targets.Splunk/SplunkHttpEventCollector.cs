using NLog.Config;
using Splunk.Logging;
using System;
using System.Collections.Generic;

namespace NLog.Targets.Splunk
{
    [Target("SplunkHttpEventCollector")]
    public sealed class SplunkHttpEventCollector : TargetWithLayout
    {
        private HttpEventCollectorSender _hecSender;

        [RequiredParameter]
        public Uri ServerUrl { get; set; }

        [RequiredParameter]
        public string Token { get; set; }

        public int RetriesOnError { get; set; } = 0;

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            if (string.IsNullOrEmpty(ServerUrl?.Authority))
            {
                throw new NLogConfigurationException("SplunkHttpEventCollector ServerUrl is not set!");
            }

            if (string.IsNullOrEmpty(Token))
            {
                throw new NLogConfigurationException("SplunkHttpEventCollector Token is not set!");
            }

            _hecSender = new HttpEventCollectorSender(
                ServerUrl,                                                                          // Splunk HEC URL
                Token,                                                                              // Splunk HEC token *GUID*
                new HttpEventCollectorEventInfo.Metadata(null, null, "_json", GetMachineName()),    // Metadata
                HttpEventCollectorSender.SendMode.Sequential,                                       // Sequential sending to keep message in order
                0,                                                                                  // BatchInterval - Set to 0 to disable
                0,                                                                                  // BatchSizeBytes - Set to 0 to disable
                0,                                                                                  // BatchSizeCount - Set to 0 to disable
                new HttpEventCollectorResendMiddleware(RetriesOnError).Plugin                       // Resend Middleware with retry
            );
        }

        protected override void Write(LogEventInfo logEventInfo)
        {
            // Sanity check for LogEventInfo
            if (logEventInfo == null)
            {
                throw new ArgumentNullException(nameof(logEventInfo));
            }

            // Make sure we have a properly setup HttpEventCollectorSender
            if (_hecSender == null)
            {
                throw new NLogRuntimeException("SplunkHttpEventCollector SendEventToServer() called before InitializeTarget()");
            }

            // Build metaData
            var metaData = new HttpEventCollectorEventInfo.Metadata(null, logEventInfo.LoggerName, "_json", GetMachineName());

            // Build properties object and add standard values
            var properties = new Dictionary<String, object>
            {
                {"Source", logEventInfo.LoggerName},
                { "Host", GetMachineName()}
            };

            // add attached properties
            if (logEventInfo.HasProperties)
            {
                foreach (var key in logEventInfo.Properties.Keys)
                {
                    properties.Add(key.ToString(), logEventInfo.Properties[key]);
                }
            }

            // add parameters
            if (logEventInfo.Parameters != null && logEventInfo.Parameters.Length > 0)
            {
                for(int i = 0; i < logEventInfo.Parameters.Length; i++)
                {
                    properties.Add("{" + i + "}", logEventInfo.Parameters[i]);
                }
            }

            // Send the event to splunk
            _hecSender.Send(null, logEventInfo.Level.Name, logEventInfo.Message, logEventInfo.FormattedMessage, logEventInfo.Exception, properties, metaData);
            _hecSender.FlushSync();
        }

        /// <summary>
        /// Gets the machine name
        /// </summary>
        /// <returns></returns>
        private string GetMachineName()
        {
            return !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("COMPUTERNAME")) ? System.Environment.GetEnvironmentVariable("COMPUTERNAME") : System.Net.Dns.GetHostName();
        }
    }
}
