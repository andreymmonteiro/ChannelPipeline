namespace Executor
{
    public static class ChannelPipelineStreamingExtensions
    {
        public static async Task StreamAsync(
            this ChannelPipeline<byte[]> pipeline,
            Stream stream,
            int chunkSize,
            CancellationToken token = default)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new InvalidOperationException("Stream is not readable.");

            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize));

            byte[] buffer = new byte[chunkSize];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                var chunk = buffer[..bytesRead];
                await pipeline.EnqueueAsync(chunk, token);
            }
        }

        public static async Task StreamFileAsync(
            this ChannelPipeline<byte[]> pipeline,
            string filePath,
            int chunkSize,
            CancellationToken token = default)
        {
            if (filePath is null)
                throw new ArgumentNullException(nameof(filePath));

            var fullPath = Path.GetFullPath(filePath);

            if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                throw new InvalidOperationException("Invalid file path.");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("The specified file does not exist.", fullPath);

            using var fs = File.OpenRead(filePath);
            await pipeline.StreamAsync(fs, chunkSize, token);
        }
    }
}
