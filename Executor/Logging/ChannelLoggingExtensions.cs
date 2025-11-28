using Microsoft.Extensions.DependencyInjection;

namespace Executor.Logging
{
    public static class ChannelLoggingExtensions
    {
        public static IServiceCollection AddChannelExecutorMicrosoftLogging(this IServiceCollection services)
        {
            services.AddScoped<IChannelLogger, ChannelMicrosoftLogger>();
            return services;
        }

        public static IServiceCollection AddChannelExecutorSerilog(this IServiceCollection services)
        {
            services.AddScoped<IChannelLogger, ChannelSerilogLogger>();
            return services;
        }
    }
}
