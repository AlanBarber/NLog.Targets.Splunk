using NLog.Config;
using Splunk.Logging;
using System;

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
            var metaData = new HttpEventCollectorEventInfo.Metadata(null, logEventInfo.LoggerName, "_json", GetMachineName());

            // Use NLog's built in tooling to get properties
            // Requires setting IncludeEventProperties="true" in target setup to return any values
            var properties = GetAllProperties(logEventInfo);

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
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMPUTERNAME")) ? Environment.GetEnvironmentVariable("COMPUTERNAME") : System.Net.Dns.GetHostName();
        }
    }
}
