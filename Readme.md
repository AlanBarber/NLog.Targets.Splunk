NLog.Targets.Splunk
===================

NLog.Targets.Splunk is a [Splunk HTTP Event Collector](http://dev.splunk.com/view/event-collector/SP-CAAAE7F) target for [NLog](http://nlog-project.org/)

[![NuGet version](https://badge.fury.io/nu/NLog.Targets.Splunk.svg)](https://badge.fury.io/nu/NLog.Targets.Splunk)

## Getting started

First you will need to have a running install of Splunk Enterprise and [setup a HTTP Event Collector](http://docs.splunk.com/Documentation/Splunk/latest/Data/UsetheHTTPEventCollector)

Then configure the SplunkHttpEventCollector with `ServerUrl` and `Token`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" >
  <extensions>
    <add assembly="NLog.Targets.Splunk" />
  </extensions>
  <targets async="true">
    <target name="Splunk"
            xsi:type="SplunkHttpEventCollector"
            serverUrl="https://splunk-server:8088"
            token="token-guid"
            channel="channel-guid"
			source="${logger}"
			sourceType="_json"
			index=""
            retriesOnError="0"
            batchSizeBytes="0"
            batchSizeCount="0"
            includeEventProperties="true"
            includePositionalParameters="false"
			includeMdlc="false"
            maxConnectionsPerServer="10"
            ignoreSslErrors="false"
			useProxy="true"
            proxyUrl="http://proxy:8888"
            proxyUser="username"
            proxyPassword="secret"            
			>
		<contextproperty name="host" layout="${machinename}" />
		<contextproperty name="threadid" layout="${threadid}" />
		<contextproperty name="logger" layout="${logger}" />
	</target>    
  </targets>
  <rules>
    <logger name="*" minlevel="Debug" writeTo="Splunk" />
  </rules>
</nlog>
```

### Understanding Configuration Options

| Option                      | Description                                                                                                                                                                 |
|-----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| serverUrl                   | The Splunk HTTP Event Collector server URL.                                                                                                                                 |
| token                       | The Splunk HTTP Event Collector token that is used for authentication to send logs.                                                                                         |
| channel                     | The Splunk HTTP Event Collector data channel.                                                                                                                               |
| source                      | The Splunk metadata source. Default value "${logger}".                                                                                                                      |
| sourceType                  | The Splunk metadata source type. Default value "_json".                                                                                                                     |
| index                       | The Splunk metadata index.                                                                                                                                                  |
| retriesOnError              | The number of retries on error. Default value "0".                                                                                                                          |
| batchSizeBytes              | The number of bytes to include before sending a batch. Default value "0" disables batching.                                                                                 |
| batchSizeCount              | The number of logevents to include before sending a batch. Default value of "0" disables batching.                                                                          |
| includeEventProperties      | Indicates whether to include per-event properties in the payload sent to the server.                                                                                        |
| includePositionalParameters | Sets whether to include positional parameters.                                                                                                                              |
| includeMdlc                 | Sets whether to include properties that are part of the [MappedDiagnosticsLogicalContext](https://github.com/NLog/NLog/wiki/Context#mappeddiagnosticslogicalcontext) that can be used to provide context-specific details.                               |
| MaxConnectionsPerServer     | Sets the maximum number of concurrent connections (per server endpoint) allowed when making requests.                                                                       |
| IgnoreSslErrors             | Sets whether to ignore SSL errors.                                                                                                                                          |
| UseProxy                    | Sets whether to use a web proxy.                                                                                                                                            |
| ProxyUrl                    | Sets Proxy URL. URL must include protocol and port, i.e. "http://proxy:5555/". If no URL specified, the default system proxy will be used, unless UseProxy is set to false. |
| ProxyUser                   | Set user name to use for authentication with proxy.                                                                                                                         |
| ProxyPassword               | Sets user password to use for authentication with proxy.                                                                                                                    |


## Feedback / Issues

Feel free to tweet [@alanbarber](http://twitter.com/alanbarber) for questions or comments on the code.  
You can also submit a GitHub issue [here](https://github.com/alanbarber/NLog.Targets.Splunk/issues).

## License

https://github.com/alanbarber/NLog.Targets.Splunk/blob/master/LICENSE
