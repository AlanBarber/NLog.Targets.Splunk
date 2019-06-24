using NLog.Layouts;
using System;

namespace NLog.Targets.Splunk.Splunk.Logging.Common
{
  public class HttpEventCollectorSenderSettings
  {
    public string RenderedToken => Token.Render(LogEventInfo.CreateNullEvent());
    public Uri RenderedUri => new Uri(Uri.Render(LogEventInfo.CreateNullEvent()));

    public Layout Uri { get; set; }
    public Layout Token { get; set; }
    public bool IgnoreSslErrors { get; set; } = false;
    public bool UseProxy { get; set; } = false;
    public int MaxConnectionsPerServer { get; set; } = 0;
  }
}
