namespace Executor.Logging
{
    public interface IChannelLogger
    {
        void LogInfo(string messageTemplate, params object?[] args);
        void LogWarning(string messageTemplate, params object?[] args);
        void LogError(string messageTemplate, Exception? ex = null, params object?[] args);
    }
}
