using System;

namespace Splunk.Logging
{
    public class ProxyConfiguration
    {
        public bool UseProxy { get; set; } = true;
        public string ProxyUrl { get; set; } = String.Empty;
        public string ProxyUser { get; set; } = String.Empty;
        public string ProxyPassword { get; set; } = String.Empty;
    }
}
