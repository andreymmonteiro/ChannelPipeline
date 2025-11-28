using Serilog;

namespace Executor.Logging
{
    public class ChannelSerilogLogger : IChannelLogger
    {
        private readonly ILogger _logger;

        public ChannelSerilogLogger()
        {
            _logger = Log.ForContext<IChannelLogger>();
        }

        public void LogInfo(string messageTemplate, params object?[] args)
            => _logger.Information(messageTemplate, args);

        public void LogWarning(string messageTemplate, params object?[] args)
            => _logger.Warning(messageTemplate, args);

        public void LogError(string messageTemplate, Exception? ex = null, params object?[] args)
            => _logger.Error(ex, messageTemplate, args);
    }
}
