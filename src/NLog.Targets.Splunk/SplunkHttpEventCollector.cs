using NLog.Config;
using Splunk.Logging;
using System;
using System.Collections.Generic;

namespace NLog.Targets.Splunk
{
    /// <summary>
    /// Splunk Http Event Collector
    /// </summary>
    /// <seealso cref="NLog.Targets.TargetWithContext" />
    [Target("SplunkHttpEventCollector")]
    public sealed class SplunkHttpEventCollector : TargetWithContext
    {
        private HttpEventCollectorSender _hecSender;

        /// <summary>
        /// Gets or sets the Splunk HTTP Event Collector server URL.
        /// </summary>
        /// <value>
        /// The Splunk HTTP Event Collector server URL.
        /// </value>
        [RequiredParameter]
        public Uri ServerUrl { get; set; }

        /// <summary>
        /// Gets or sets the Splunk HTTP Event Collector token.
        /// </summary>
        /// <value>
        /// The SPlunk HTTP Event Collector token.
        /// </value>
        [RequiredParameter]
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the number of retries on error.
        /// </summary>
        /// <value>
        /// The number of retries on error.
        /// </value>
        public int RetriesOnError { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether to include positional parameters
        /// </summary>
        public bool IncludePositionalParameters { get; set; }

        /// <summary>
        /// Configuration of additional properties to include with each LogEvent (Ex. ${logger}, ${machinename}, ${threadid} etc.)
        /// </summary>
        public override IList<TargetPropertyWithContext> ContextProperties { get; } = new List<TargetPropertyWithContext>();

        Dictionary<string, HttpEventCollectorEventInfo.Metadata> _metaData = new Dictionary<string, HttpEventCollectorEventInfo.Metadata>();

        private string _hostName;

        public SplunkHttpEventCollector()
        {
            OptimizeBufferReuse = true;
            IncludeEventProperties = true;
            Layout = "${message}";
        }

        /// <summary>
        /// Initializes the target. Can be used by inheriting classes
        /// to initialize logging.
        /// </summary>
        /// <exception cref="NLogConfigurationException">
        /// SplunkHttpEventCollector ServerUrl is not set!
        /// or
        /// SplunkHttpEventCollector Token is not set!
        /// </exception>
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

            _metaData.Clear();

            _hecSender = new HttpEventCollectorSender(
                ServerUrl,                                                                          // Splunk HEC URL
                Token,                                                                              // Splunk HEC token *GUID*
                GetMetaData(null),                                                                  // Metadata
                HttpEventCollectorSender.SendMode.Sequential,                                       // Sequential sending to keep message in order
                0,                                                                                  // BatchInterval - Set to 0 to disable
                0,                                                                                  // BatchSizeBytes - Set to 0 to disable
                0,                                                                                  // BatchSizeCount - Set to 0 to disable
                new HttpEventCollectorResendMiddleware(RetriesOnError).Plugin                       // Resend Middleware with retry
            );
            _hecSender.OnError += (e) => { Common.InternalLogger.Error(e, "SplunkHttpEventCollector(Name={0}): Failed to send LogEvents", Name); };
        }

        /// <summary>
        /// Writes the specified log event information.
        /// </summary>
        /// <param name="logEventInfo">The log event information.</param>
        /// <exception cref="System.ArgumentNullException">logEventInfo</exception>
        /// <exception cref="NLogRuntimeException">SplunkHttpEventCollector SendEventToServer() called before InitializeTarget()</exception>
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

            // Build MetaData
            var metaData = GetMetaData(logEventInfo.LoggerName);

            // Use NLog's built in tooling to get properties
            var properties = GetAllProperties(logEventInfo);

            if (IncludePositionalParameters && logEventInfo.Parameters != null)
            {
                for (int i = 0; i < logEventInfo.Parameters.Length; ++i)
                {
                    properties[string.Concat("{", i.ToString(), "}")] = logEventInfo.Parameters[i];
                }
            }

            // Send the event to splunk
            string renderedMessage = RenderLogEvent(Layout, logEventInfo);
            _hecSender.Send(null, logEventInfo.Level.Name, logEventInfo.Message, renderedMessage, logEventInfo.Exception, properties, metaData);
            _hecSender.FlushSync();
        }

        private HttpEventCollectorEventInfo.Metadata GetMetaData(string loggerName)
        {
            var hostName = _hostName ?? (_hostName = GetMachineName());
            if (!_metaData.TryGetValue(loggerName ?? string.Empty, out var metaData))
            {
                if (_metaData.Count > 1000)
                    _metaData.Clear();  // Extreme case that should never happen
                metaData = new HttpEventCollectorEventInfo.Metadata(null, string.IsNullOrEmpty(loggerName) ? null : loggerName, "_json", hostName);
                _metaData[loggerName ?? string.Empty] = metaData;
            }

            return metaData;
        }

        /// <summary>
        /// Gets the machine name
        /// </summary>
        private static string GetMachineName()
        {
            return TryLookupValue(() => Environment.GetEnvironmentVariable("COMPUTERNAME"), "COMPUTERNAME")
                ?? TryLookupValue(() => Environment.GetEnvironmentVariable("HOSTNAME"), "HOSTNAME")
                ?? TryLookupValue(() => Environment.MachineName, "MachineName")
                ?? TryLookupValue(() => System.Net.Dns.GetHostName(), "DnsHostName");
        }

        private static string TryLookupValue(Func<string> lookupFunc, string lookupType)
        {
            try
            {
                string lookupValue = lookupFunc()?.Trim();
                return string.IsNullOrEmpty(lookupValue) ? null : lookupValue;
            }
            catch (Exception ex)
            {
                NLog.Common.InternalLogger.Warn(ex, "SplunkHttpEventCollector(Name={0}): Failed to lookup {1}", lookupType);
                return null;
            }
        }
    }
}
