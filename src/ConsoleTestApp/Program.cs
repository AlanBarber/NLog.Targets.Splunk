using System;

namespace ConsoleTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SplunkHttpEventCollector NLog Target Test App");
            
            // Get a logger instance
            var logger = NLog.LogManager.GetCurrentClassLogger();

            // Validate logger instance is loaded and display configuration
            Console.WriteLine("Logger Settings:");
            Console.WriteLine($"  Name:           {logger.Name}");
            Console.WriteLine($"  IsFatalEnabled: {logger.IsFatalEnabled}");
            Console.WriteLine($"  IsErrorEnabled: {logger.IsErrorEnabled}");
            Console.WriteLine($"  IsWarnEnabled:  {logger.IsWarnEnabled}");
            Console.WriteLine($"  IsInfoEnabled:  {logger.IsInfoEnabled}");
            Console.WriteLine($"  IsDebugEnabled: {logger.IsDebugEnabled}");
            Console.WriteLine($"  IsTraceEnabled: {logger.IsTraceEnabled}");

            Console.WriteLine("  Targets:");
            foreach (var t in logger.Factory.Configuration.AllTargets)
            {
                Console.WriteLine($"    {t.Name}");
            }

            Console.WriteLine("  Rules:");
            foreach (var r in logger.Factory.Configuration.LoggingRules)
            {
                Console.WriteLine($"    Name: {r.LoggerNamePattern}, Targets: {string.Join(",", r.Targets)}, Levels: {string.Join(",", r.Levels)}");
            }

            Console.WriteLine("Writting log messages...");
            // Write a few messages
            logger.Trace("This is a trace log message");
            logger.Debug("This is a debug log message");
            logger.Info("This is an info log message");
            logger.Warn("This is a warn log message");
            logger.Error("This is an error log message");
            logger.Fatal("This is a fatal log message");

            // Process an exception
            try
            {
                throw new Exception();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Our pretend exception detected!");
            }



            #if DEBUG
            Console.Write("\nPress any key to continue...");
            Console.ReadKey();
            #endif
        }
    }
}
