using Executor.Logging;
using Moq;

namespace Executor.UnitTests
{
    public class ChannelPipelineTest
    {
        [Fact]
        public async Task Should_Process_All_Items()
        {
            // arrange
            var processed = new List<int>();
            Func<int, Task> handler = x =>
            {
                processed.Add(x);
                return Task.CompletedTask;
            };

            var pipeline = ChannelPipeline<int>.Create(parallelism: 2, handler);

            // act
            await pipeline.EnqueueAsync(1);
            await pipeline.EnqueueAsync(2);
            await pipeline.EnqueueAsync(3);

            pipeline.Complete();
            await pipeline.Completion;

            // assert
            Assert.Equal(new[] { 1, 2, 3 }, processed);
        }

        [Fact]
        public async Task Should_Not_Stop_On_Handler_Exception()
        {
            // arrange
            int successCount = 0;

            Func<int, Task> handler = x =>
            {
                if (x == 2) throw new Exception("boom");
                successCount++;
                return Task.CompletedTask;
            };

            var pipeline = ChannelPipeline<int>.Create(1, handler);

            // act
            await pipeline.EnqueueAsync(1);
            await pipeline.EnqueueAsync(2);
            await pipeline.EnqueueAsync(3);

            pipeline.Complete();
            await pipeline.Completion;

            // assert
            Assert.Equal(2, successCount);
        }

        [Fact]
        public async Task Should_Stream_File_In_Chunks_Through_Pipeline()
        {
            // arrange
            byte[] fakeFile = new byte[10_000];
            new Random(42).NextBytes(fakeFile);

            using var ms = new MemoryStream(fakeFile);

            const int chunkSize = 1024;
            int totalProcessed = 0;

            // consumer handler: process chunk
            Func<byte[], Task> handler = async chunk =>
            {
                // simulate processing cost
                await Task.Delay(1);
                totalProcessed += chunk.Length;
            };

            var pipeline = ChannelPipeline<byte[]>.Create(
                parallelism: 4,
                handler: handler
            );

            await pipeline.StreamAsync(ms, chunkSize);

            pipeline.Complete();
            await pipeline.Completion;

            // assert
            Assert.Equal(fakeFile.Length, totalProcessed);
        }

        [Fact]
        public async Task StreamFileAsync_Should_Process_All_File_Bytes()
        {
            // arrange
            var tempFile = Path.GetTempFileName();
            byte[] fakeData = Enumerable.Range(0, 5000).Select(i => (byte)(i % 256)).ToArray();
            await File.WriteAllBytesAsync(tempFile, fakeData);

            int processedBytes = 0;
            var pipeline = ChannelPipeline<byte[]>.Create(2, async chunk =>
            {
                processedBytes += chunk.Length;
            });

            // act
            await pipeline.StreamFileAsync(tempFile, 1024);

            pipeline.Complete();
            await pipeline.Completion;

            // assert
            Assert.Equal(fakeData.Length, processedBytes);
        }

        [Fact]
        public async Task StreamFileAsync_Should_Throw_When_File_Not_Found()
        {
            var pipeline = ChannelPipeline<byte[]>.Create(1, _ => Task.CompletedTask);

            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await pipeline.StreamFileAsync("non_existing_file_654321.bin", 1024);
            });
        }

        [Fact]
        public async Task StreamFileAsync_Should_Reject_Invalid_Characters()
        {
            var pipeline = ChannelPipeline<byte[]>.Create(1, _ => Task.CompletedTask);

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await pipeline.StreamFileAsync("my|bad<path>.txt", 512);
            });
        }

        [Fact]
        public async Task StreamFileAsync_Should_Cancel_Early()
        {
            // arrange
            var tempFile = Path.GetTempFileName();
            byte[] data = new byte[20_000];
            new Random().NextBytes(data);
            await File.WriteAllBytesAsync(tempFile, data);

            int chunksProcessed = 0;

            var cts = new CancellationTokenSource();

            var pipeline = ChannelPipeline<byte[]>.Create(2, async chunk =>
            {
                chunksProcessed++;
                await Task.Delay(10);
            });

            // act
            var streamingTask = pipeline.StreamFileAsync(tempFile, 1024, cts.Token);

            // cancel quickly
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await streamingTask;
            });
        }

        [Fact]
        public async Task StreamFileAsync_Should_Log_On_Each_Chunk()
        {
            // arrange
            var logger = new Mock<IChannelLogger>();
            var pipeline = ChannelPipeline<byte[]>.Create(1, _ => Task.CompletedTask, logger: logger.Object);

            var tempFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFile, new byte[3000]); // 3 chunks with size 1024

            // act
            await pipeline.StreamFileAsync(tempFile, 1024);

            pipeline.Complete();
            await pipeline.Completion;

            // assert at least 3 logs from enqueue
            logger.Verify(
                l => l.LogInfo(It.IsAny<string>(), It.IsAny<object[]>()),
                Times.AtLeast(3)
            );
        }

        [Fact]
        public async Task StreamFileAsync_Should_Handle_Last_Small_Chunk()
        {
            // arrange
            var temp = Path.GetTempFileName();

            // 2500 bytes gives:
            // 1024, 1024, 452
            byte[] data = new byte[2500];
            await File.WriteAllBytesAsync(temp, data);

            var chunkSizes = new List<int>();

            var pipeline = ChannelPipeline<byte[]>.Create(1, async chunk =>
            {
                chunkSizes.Add(chunk.Length);
                await Task.CompletedTask;
            });

            // act
            await pipeline.StreamFileAsync(temp, 1024);

            pipeline.Complete();
            await pipeline.Completion;

            // assert
            Assert.Equal(3, chunkSizes.Count);
            Assert.Equal(new[] { 1024, 1024, 452 }, chunkSizes);
        }

        [Fact]
        public async Task StreamFileAsync_Should_Throw_For_Invalid_ChunkSize()
        {
            var temp = Path.GetTempFileName();

            byte[] data = new byte[2500];
            await File.WriteAllBytesAsync(temp, data);

            var pipeline = ChannelPipeline<byte[]>.Create(1, _ => Task.CompletedTask);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await pipeline.StreamFileAsync(temp, 0);
            });
        }

        [Fact]
        public async Task StreamFileAsync_Should_Handle_Empty_File()
        {
            var tempFile = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFile, Array.Empty<byte>());

            var chunks = 0;

            var pipeline = ChannelPipeline<byte[]>.Create(1, chunk =>
            {
                chunks++;
                return Task.CompletedTask;
            });

            await pipeline.StreamFileAsync(tempFile, 1024);

            pipeline.Complete();
            await pipeline.Completion;

            Assert.Equal(0, chunks);
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task StreamAsync_Should_Handle_100k_Chunks()
        {
            // arrange
            const int chunkSize = 1024;
            const int totalChunks = 100_000;
            const int totalBytes = chunkSize * totalChunks;

            byte[] allBytes = new byte[totalBytes];
            new Random(123).NextBytes(allBytes);

            using var ms = new MemoryStream(allBytes);

            int processedBytes = 0;

            var pipeline = ChannelPipeline<byte[]>.Create(
                parallelism: 8,
                handler: async chunk =>
                {
                    Interlocked.Add(ref processedBytes, chunk.Length);
                    await Task.Yield();
                },
                capacity: 5000  // backpressure stress
            );

            // act
            await pipeline.StreamAsync(ms, chunkSize);

            pipeline.Complete();
            await pipeline.Completion;

            // assert
            Assert.Equal(totalBytes, processedBytes);
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task Pipeline_Should_Handle_Fast_Producer_And_Slow_Consumer()
        {
            // arrange
            var data = new byte[5_000_000]; // 5MB
            new Random().NextBytes(data);

            using var ms = new MemoryStream(data);

            int processedChunks = 0;

            var pipeline = ChannelPipeline<byte[]>.Create(
                parallelism: 2,
                handler: async chunk =>
                {
                    Interlocked.Increment(ref processedChunks);
                    await Task.Delay(2); // slow consumer
                },
                capacity: 100 // force waiting
            );

            // act
            await pipeline.StreamAsync(ms, chunkSize: 2048);

            pipeline.Complete();
            await pipeline.Completion;

            // assert
            Assert.True(processedChunks > 0);
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task Pipeline_Should_Run_Stress_With_Logger()
        {
            var logger = new Mock<IChannelLogger>();

            byte[] data = new byte[5_000_000];
            new Random().NextBytes(data);
            using var ms = new MemoryStream(data);

            long processed = 0;

            var pipeline = ChannelPipeline<byte[]>.Create(
                parallelism: 8,
                handler: async chunk =>
                {
                    Interlocked.Add(ref processed, chunk.Length);
                    await Task.Yield();
                },
                capacity: 3000,
                logger: logger.Object
            );

            await pipeline.StreamAsync(ms, 4096);

            pipeline.Complete();
            await pipeline.Completion;

            Assert.Equal(data.Length, processed);
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task Pipeline_Should_Handle_Multiple_Parallel_File_Streams()
        {
            // arrange: 4 fake files in memory
            var files = Enumerable.Range(0, 4)
                .Select(_ => {
                    byte[] data = new byte[3_000_000];
                    new Random().NextBytes(data);
                    return new MemoryStream(data);
                })
                .ToList();

            long total = 0;

            var pipeline = ChannelPipeline<byte[]>.Create(
                parallelism: 8,
                handler: async chunk =>
                {
                    Interlocked.Add(ref total, chunk.Length);
                    await Task.Yield();
                },
                capacity: 5000
            );

            // act
            var tasks = files.Select(fs => pipeline.StreamAsync(fs, 4096));
            await Task.WhenAll(tasks);

            pipeline.Complete();
            await pipeline.Completion;

            long expected = files.Sum(f => f.Length);
            Assert.Equal(expected, total);
        }

        [Fact]
        [Trait("Category", "Stress")]
        public async Task Pipeline_Should_Handle_Bursty_Producer()
        {
            const int burstCount = 10_000;
            var rand = new Random();

            var chunks = Enumerable.Range(0, burstCount)
                .Select(_ => {
                    byte[] buffer = new byte[1024];
                    rand.NextBytes(buffer);
                    return buffer;
                }).ToList();

            int processed = 0;

            var pipeline = ChannelPipeline<byte[]>.Create(
                parallelism: 4,
                handler: async chunk =>
                {
                    Interlocked.Add(ref processed, chunk.Length);
                    await Task.Yield();
                },
                capacity: 50
            );

            // Burst producer
            foreach (var chunk in chunks)
                await pipeline.EnqueueAsync(chunk);

            pipeline.Complete();
            await pipeline.Completion;

            Assert.Equal(burstCount * 1024, processed);
        }

    }
}