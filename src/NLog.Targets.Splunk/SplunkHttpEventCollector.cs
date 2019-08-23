using NLog.Common;
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
        /// The Splunk HTTP Event Collector token.
        /// </value>
        [RequiredParameter]
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the optional Splunk HTTP Event Collector data channel.
        /// </summary>
        /// <value>
        /// The Splunk HTTP Event Collector data channel.
        /// </value>
        public string Channel { get; set; }

        /// <summary>
        /// Gets or sets the number of retries on error.
        /// </summary>
        /// <value>
        /// The number of retries on error.
        /// </value>
        public int RetriesOnError { get; set; } = 0;

        /// <summary>
        /// Gets or sets the number of bytes to include before sending a batch
        /// </summary>
        /// <value>
        /// The batch size in bytes.
        /// </value>
        public int BatchSizeBytes { get; set; } = 0;    // 0 = No batching

        /// <summary>
        /// Gets or sets the number of logevents to include before sending a batch
        /// </summary>
        /// <value>
        /// The batch size count.
        /// </value>
        public int BatchSizeCount { get; set; } = 0;    // 0 = No batching

        /// <summary>
        /// Gets or sets whether to include positional parameters
        /// </summary>
        /// <value>
        ///   <c>true</c> if [include positional parameters]; otherwise, <c>false</c>.
        /// </value>
        public bool IncludePositionalParameters { get; set; }

        /// <summary>
        /// Ignore SSL errors when using homemade Ssl Certificates
        /// </summary>
        /// <value>
        ///   <c>true</c> if [ignore SSL errors]; otherwise, <c>false</c>.
        /// </value>
        public bool IgnoreSslErrors { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections (per server endpoint) allowed when making requests
        /// </summary>
        /// <value>0 = Use default limit. Default = 10</value>
        public int MaxConnectionsPerServer { get; set; } = 10;

        public bool UseHttpVersion10Hack { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to use default web proxy.
        /// </summary>
        /// <value>
        /// <c>true</c> = use default web proxy. <c>false</c> = use no proxy. Default is <c>true</c> 
        /// </value>
        public bool UseProxy { get; set; } = true;

        /// <summary>
        /// Gets or sets Proxy URL
        /// <value>Default is empty. URL must include protocol and port, i.e. <code>http://proxy:5555/</code>.
        /// If no URL specified, the default system proxy will be used, unless UseProxy is set to false.</value>
        /// </summary>
        public string ProxyUrl { get; set; } = String.Empty;

        /// <summary>
        /// Gets or set user name to use for authentication with proxy
        /// </summary>
        public string ProxyUser { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets user password to use for authentication with proxy
        /// </summary>
        public String ProxyPassword { get; set; } = String.Empty;

        /// <summary>
        /// Configuration of additional properties to include with each LogEvent (Ex. ${logger}, ${machinename}, ${threadid} etc.)
        /// </summary>
        public override IList<TargetPropertyWithContext> ContextProperties { get; } = new List<TargetPropertyWithContext>();

        Dictionary<string, HttpEventCollectorEventInfo.Metadata> _metaData = new Dictionary<string, HttpEventCollectorEventInfo.Metadata>();

        private string _hostName;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplunkHttpEventCollector"/> class.
        /// </summary>
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
            NLog.Common.InternalLogger.Debug("Initializing SplunkHttpEventCollector");

            if (string.IsNullOrEmpty(ServerUrl?.Authority))
            {
                throw new NLogConfigurationException("SplunkHttpEventCollector ServerUrl is not set!");
            }

            if (string.IsNullOrEmpty(Token))
            {
                throw new NLogConfigurationException("SplunkHttpEventCollector Token is not set!");
            }

            _metaData.Clear();
            var proxyConfig = UseProxy
                ? new ProxyConfiguration
                {
                    ProxyUrl = ProxyUrl, ProxyUser = ProxyUser, ProxyPassword = ProxyPassword
                }
                : new ProxyConfiguration{UseProxy = false}; 
            _hecSender = new HttpEventCollectorSender(
                ServerUrl,                                                                          // Splunk HEC URL
                Token,                                                                              // Splunk HEC token *GUID*
                Channel,                                                                            // Splunk HEC data channel *GUID*
                GetMetaData(null),                                                                  // Metadata
                HttpEventCollectorSender.SendMode.Sequential,                                       // Sequential sending to keep message in order
                BatchSizeBytes == 0 && BatchSizeCount == 0 ? 0 : 250,                               // BatchInterval - Set to 0 to disable
                BatchSizeBytes,                                                                     // BatchSizeBytes - Set to 0 to disable
                BatchSizeCount,                                                                     // BatchSizeCount - Set to 0 to disable
                IgnoreSslErrors,                                                                    // Enable Ssl Error ignore for self singed certs *BOOL*
                proxyConfig,                                                                           // UseProxy - Set to false to disable
                MaxConnectionsPerServer,
                new HttpEventCollectorResendMiddleware(RetriesOnError).Plugin,                      // Resend Middleware with retry
                httpVersion10Hack: UseHttpVersion10Hack
            );
            _hecSender.OnError += (e) => { InternalLogger.Error(e, "SplunkHttpEventCollector(Name={0}): Failed to send LogEvents", Name); };
        }

        /// <summary>
        /// Disposes the initialized HttpEventCollectorSender
        /// </summary>
        protected override void CloseTarget()
        {
            try
            {
                _hecSender?.Dispose();
                base.CloseTarget();
            }
            finally
            {
                _hecSender = null;
            }
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
                for (var i = 0; i < logEventInfo.Parameters.Length; ++i)
                {
                    properties[string.Concat("{", i.ToString(), "}")] = logEventInfo.Parameters[i];
                }
            }

            // Send the event to splunk
            string renderedMessage = RenderLogEvent(Layout, logEventInfo);
            _hecSender.Send(null, logEventInfo.Level.Name, logEventInfo.Message, renderedMessage, logEventInfo.Exception, properties, metaData);
            if (BatchSizeBytes == 0 && BatchSizeCount == 0)
            {
                _hecSender.FlushSync();
            }
        }

        /// <summary>
        /// Flush any pending log messages asynchronously (in case of asynchronous targets).
        /// </summary>
        /// <param name="asyncContinuation">The asynchronous continuation.</param>
        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            try
            {
                _hecSender?.FlushSync();
                asyncContinuation(null);
            }
            catch (Exception ex)
            {
                asyncContinuation(ex);
            }
        }

        /// <summary>
        /// Gets the meta data.
        /// </summary>
        /// <param name="loggerName">Name of the logger.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Tries the lookup value.
        /// </summary>
        /// <param name="lookupFunc">The lookup function.</param>
        /// <param name="lookupType">Type of the lookup.</param>
        /// <returns></returns>
        private static string TryLookupValue(Func<string> lookupFunc, string lookupType)
        {
            try
            {
                string lookupValue = lookupFunc()?.Trim();
                return string.IsNullOrEmpty(lookupValue) ? null : lookupValue;
            }
            catch (Exception ex)
            {
                NLog.Common.InternalLogger.Warn(ex, "SplunkHttpEventCollector: Failed to lookup {0}", lookupType);
                return null;
            }
        }
    }
}
