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
            retriesOnError="0"
            batchSizeBytes="0"
            batchSizeCount="0"
            includeEventProperties="true"
            includeMdlc="false"
            includePositionalParameters="false"
	    MaxConnectionsPerServer="10"
	    IgnoreSslErrors="false">
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

## Feedback / Issues

Feel free to tweet [@alanbarber](http://twitter.com/alanbarber) for questions or comments on the code.  
You can also submit a GitHub issue [here](https://github.com/alanbarber/NLog.Targets.Splunk/issues).

## License

https://github.com/alanbarber/NLog.Targets.Splunk/blob/master/LICENSE

## Release Notes

### Verison 2.2.1

- Introduced MaxConnectionsPerServer property to limit number of connections to splunk server by Rolf Kristensen (https://github.com/snakefoot)

### Version 2.2.0

- Updated NLog version from 4.5.6 to 4.5.9
- Added support for Splunk HEC Data channel by David Matz (https://github.com/davidmatz)

### Version 2.1.0

- Updated NLog version from 4.5.3 to 4.5.6
- Adding 'IgnoreSslErrors' property to config to allow for using Splunk with self signed certs
- updated to work with NLog's paremeter tooling
- adding abilty to toggle logging of parameters on or off
- several performance updates
- Speical thanks to Rolf Kristensen (https://github.com/snakefoot) for help with this release!

### Version 2.0.0

- Updated to support .Net Standard 2.0 and .Net 4.5 w/ NLog 4.5.3

### Version 1.0.0

- Code cleanup and performance improvements

### Verison 0.0.1

- First publish to NuGet
