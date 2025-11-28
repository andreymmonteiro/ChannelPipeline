using Microsoft.Extensions.Logging;

namespace Executor.Logging
{
    public class ChannelMicrosoftLogger : IChannelLogger
    {
        private readonly ILogger<IChannelLogger> _logger;

        public ChannelMicrosoftLogger(ILogger<IChannelLogger> logger)
        {
            _logger = logger;
        }

        public void LogInfo(string messageTemplate, params object?[] args)
            => _logger.LogInformation(messageTemplate, args);

        public void LogWarning(string messageTemplate, params object?[] args)
            => _logger.LogWarning(messageTemplate, args);

        public void LogError(string messageTemplate, Exception? ex = null, params object?[] args)
            => _logger.LogError(ex, messageTemplate, args);
    }
}
