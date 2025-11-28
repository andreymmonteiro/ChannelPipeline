using Executor.Logging;
using System.Threading.Channels;

namespace Executor
{
    public sealed class ChannelPipeline<T>
    {
        private readonly Channel<T> _channel;
        private readonly Task[] _workers;
        private readonly Func<T, Task> _handler;
        private readonly IChannelLogger? _logger;

        public Task Completion => Task.WhenAll(_workers);

        private ChannelPipeline(
            int parallelism,
            int capacity,
            Func<T, Task> handler,
            BoundedChannelFullMode fullMode,
            CancellationToken cancellationToken,
            IChannelLogger? logger)
        {
            if (parallelism <= 0)
                throw new ArgumentOutOfRangeException(nameof(parallelism));

            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _logger = logger;

            _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = fullMode
            });

            _workers = Enumerable.Range(0, parallelism)
                .Select(_ => Task.Run(() => WorkerLoop(cancellationToken), cancellationToken))
                .ToArray();

            _logger?.LogInfo("ChannelPipeline started with {Parallelism} workers and capacity {Capacity}",
                parallelism, capacity);
        }

        private async Task WorkerLoop(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        _logger?.LogInfo("Processing item {@Item}", item);
                        await _handler(item);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            "Handler failed for item {@Item}",
                            ex,
                            item
                        );
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("ChannelPipeline worker loop canceled");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Worker loop failed unexpectedly", ex);
            }
        }

        public static ChannelPipeline<T> Create(
            int parallelism,
            Func<T, Task> handler,
            int capacity = 100,
            BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
            CancellationToken cancellationToken = default,
            IChannelLogger? logger = null)
        {
            return new ChannelPipeline<T>(
                parallelism,
                capacity,
                handler,
                fullMode,
                cancellationToken,
                logger);
        }

        public ValueTask EnqueueAsync(T item, CancellationToken token = default)
        {
            _logger?.LogInfo("Enqueuing item {@Item}", item);
            return _channel.Writer.WriteAsync(item, token);
        }

        public void Complete()
        {
            _logger?.LogInfo("Completing ChannelPipeline writer");
            _channel.Writer.Complete();
        }

        public async Task CompleteAndWaitAsync()
        {
            Complete();
            _logger?.LogInfo("Waiting for ChannelPipeline completion");
            await Completion;
        }
    }
}

