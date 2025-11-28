# 📦 ChannelPipeline  
### A Modern Producer/Consumer Pipeline for .NET

`ChannelPipeline<T>` is a lightweight, high-performance abstraction built on `System.Threading.Channels` that simplifies building concurrent producer/consumer workflows in .NET.

It removes all the channel boilerplate and gives you a clean, ergonomic API for:

- Async parallel message processing  
- Backpressure  
- Logging (optional, pluggable)  
- File & stream chunking  
- Clean completion lifecycle  

---

## 🚀 Features

- ⚡ High-performance async pipeline  
- 🔄 Parallel worker tasks  
- 📦 Bounded channels with backpressure  
- 🧵 Clean producer/consumer model  
- 🧱 Simple public API  
- 🪵 Optional structured logging  
- 📥 File & Stream chunk ingestion (`byte[]`)  
- 🛡️ Automatic exception isolation  
- 🧪 Unit & stress tested  

---

# 🧠 Why ChannelPipeline?

Using `System.Threading.Channels` directly is powerful but verbose.  
You need to manually implement:

- channel creation  
- bounded capacity settings  
- worker loops  
- async error handling  
- concurrency control  
- completion signaling  
- logging  

`ChannelPipeline<T>` wraps all that into a clean abstraction so you focus only on your business logic.

---

# 🏗️ How It Works

A `ChannelPipeline<T>` has three parts:

## 1️⃣ Producer  
Push items into the pipeline:

```csharp
await pipeline.EnqueueAsync(item);
```

## 2️⃣ Workers  
A configurable number of workers consume items:

```csharp
handler: async item => { /* your logic */ }
```

Workers process independently using async/await.

## 3️⃣ Completion  
Stop accepting new items:

```csharp
pipeline.Complete();
await pipeline.Completion;
```

Or in one step:

```csharp
await pipeline.CompleteAndWaitAsync();
```

---

# 🛠️ Creating a Pipeline

```csharp
var pipeline = ChannelPipeline<int>.Create(
    parallelism: 4,
    handler: async value =>
    {
        Console.WriteLine($"Processing {value}");
        await Task.Delay(100);
    }
);
```

---

# ➕ Enqueuing Items

```csharp
await pipeline.EnqueueAsync(10);
await pipeline.EnqueueAsync(20);
await pipeline.EnqueueAsync(30);
```

---

# 🏁 Completing the Pipeline

### Manual

```csharp
pipeline.Complete();
await pipeline.Completion;
```

### Convenience

```csharp
await pipeline.CompleteAndWaitAsync();
```

---

# 📝 Logging (Optional)

Inject any logging provider using `IChannelLogger`:

```csharp
public interface IChannelLogger
{
    void LogInfo(string template, params object?[] args);
    void LogWarning(string template, params object?[] args);
    void LogError(string template, Exception? ex = null, params object?[] args);
}
```

Adapters can wrap:

- Serilog  
- Microsoft.Extensions.Logging  
- Custom logs  

---

# 📥 Streaming Files & Streams (`byte[]` Pipelines)

Stream files in chunks:

```csharp
await pipeline.StreamFileAsync("input.bin", 4096);
```

Stream any stream:

```csharp
await pipeline.StreamAsync(networkStream, 8192);
```

Pipeline handler example:

```csharp
var pipeline = ChannelPipeline<byte[]>.Create(
    4, 
    async chunk => 
    {
        Console.WriteLine($"Chunk size: {chunk.Length}");
    }
);
```

---

# 🧪 Error Handling

- Handler exceptions are logged  
- Workers continue processing  
- Pipeline remains operational  

This provides robustness for long-running jobs.

---

# 🧵 Worker Loop Behavior

```csharp
await foreach (var item in _channel.Reader.ReadAllAsync())
{
    try
    {
        await handler(item);
    }
    catch (Exception ex)
    {
        logger?.LogError("Handler failed for item {@Item}", ex, item);
    }
}
```

---

# 📚 Use Cases

- File & media processing  
- ETL pipelines  
- Background jobs  
- High-throughput ingestion  
- Network streaming  
- CPU-bound parallel workloads  
- Microservices background tasks  

---

# ❤️ Summary

ChannelPipeline gives you:

- Clean API  
- High performance  
- Backpressure  
- Logging support  
- Stream chunking  
- Reliable worker concurrency  
- Automatic error isolation  

**Focus on your logic — the pipeline handles the plumbing.**