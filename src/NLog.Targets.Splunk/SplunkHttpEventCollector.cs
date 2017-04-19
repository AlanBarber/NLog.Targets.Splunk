using NLog.Config;
using Splunk.Logging;
using System;
using System.Dynamic;
using System.Net;

namespace NLog.Targets.Splunk
{
    [Target("SplunkHttpEventCollector")]
    public sealed class SplunkHttpEventCollector : TargetWithLayout
    {
        [RequiredParameter]
        public Uri ServerUrl { get; set; }

        [RequiredParameter]
        public string Token { get; set; }

        public int RetriesOnError { get; set; } = 0;

        public bool IgnoreSslErrors { get; set; } = false;

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
        }

        protected override void Write(LogEventInfo logEvent)
        {
            SendEventToServer(logEvent);
        }

        private void SendEventToServer(LogEventInfo logEvent)
        {
            var hecSender = new HttpEventCollectorSender(
                ServerUrl,                                                                                             // Splunk HEC URL
                Token,                                                                                                 // Splunk HEC token *GUID*
                new HttpEventCollectorEventInfo.Metadata(null, logEvent.LoggerName, "_json", Environment.MachineName), // Metadata
                HttpEventCollectorSender.SendMode.Sequential,                                                          // Sequential sending to keep message in order
                0,                                                                                                     // BatchInterval - Set to 0 to disable
                0,                                                                                                     // BatchSizeBytes - Set to 0 to disable
                0,                                                                                                     // BatchSizeCount - Set to 0 to disable
                new HttpEventCollectorResendMiddleware(RetriesOnError).Plugin                                          // Resend Middleware with retry
            );

            // throw error on send failure
            hecSender.OnError += exception =>
            {
                throw new NLogRuntimeException($"SplunkHttpEventCollector failed to send log event to Splunk server '{ServerUrl?.Authority}' using token '{Token}'. Exception: {exception}");
            };

            // If enabled will create callback to bypass ssl error checks for our server url
            if (IgnoreSslErrors)
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    var httpWebRequest = sender as HttpWebRequest;
                    return httpWebRequest?.RequestUri.Authority == ServerUrl.Authority;
                };
            }

            // Build optional data object
            dynamic objData = null;

            if (logEvent.Exception != null || logEvent.HasProperties)
            {
                objData = new ExpandoObject();

                if (logEvent.Exception != null)
                {
                    objData.Exception = logEvent.Exception;
                }

                if (logEvent.HasProperties)
                {
                    objData.Properties = logEvent.Properties;
                }

            }
            
            // Send the event to splunk
            hecSender.Send(Guid.NewGuid().ToString(), logEvent.Level.Name, Layout.Render(logEvent), objData);
            hecSender.FlushSync();
        }
    }
}
