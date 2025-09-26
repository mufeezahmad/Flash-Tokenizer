using System;
using System.Diagnostics;
using System.IO;
using FlashTokenizer;

class Program
{
	static async Task Main(string[] args)
	{
		try
		{
			Console.WriteLine("Starting FlashTokenizer benchmark...");
			// Accept file path as first argument if provided; otherwise use default.
			string defaultPath = @"C:\Users\Mufeez.Ahmad\Downloads\jazz_pakistan_faq_Copy.md";
			string filePath = (args.Length > 0 && File.Exists(args[0])) ? args[0] : defaultPath;
			if (!File.Exists(filePath)) { Console.WriteLine($"File not found: {filePath}"); return; }

			string vocabPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample", "vocab.txt"));
			Console.WriteLine($"Vocab path: {vocabPath}");
			Console.WriteLine($"Vocab exists: {File.Exists(vocabPath)}");
			
			// Run different benchmarks based on arguments.
			// If a file path was supplied as first arg, modes may start from args[1].
			int modeIndex = (args.Length > 0 && File.Exists(args[0])) ? 1 : 0;
			if (args.Length > modeIndex)
			{
				switch (args[modeIndex].ToLower())
				{
					case "parallel":
						await RunParallelBenchmark(filePath, vocabPath);
						break;
					case "async":
						await RunAsyncBenchmark(filePath, vocabPath);
						break;
					case "compare":
						await RunComparisonBenchmark(filePath, vocabPath);
						break;
					default:
						RunOptimizedBenchmark(filePath, vocabPath);
						break;
				}
			}
			else
			{
				// Default: run optimized benchmark
				RunOptimizedBenchmark(filePath, vocabPath);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
		}
	}

	static void RunOptimizedBenchmark(string filePath, string vocabPath)
	{
		string text = File.ReadAllText(filePath);
		Console.WriteLine($"📄 File: {Path.GetFileName(filePath)} ({text.Length:N0} chars)");
		Console.WriteLine("🔧 Testing: Optimized Sequential Tokenizer");

		// Test optimized tokenizer
		var tokenizer = new FlashBertTokenizerOptimized(vocabPath, doLowerCase: true, modelMaxLength: -1);

		// Warmup
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
		var warmupIds = tokenizer.Encode(text.Substring(0, Math.Min(1000, text.Length)));

		// Benchmark
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
		var sw = Stopwatch.StartNew();
		var ids = tokenizer.Encode(text, padding: "longest", maxLength: -1);
		sw.Stop();

		Console.WriteLine($"🧮 Tokens: {ids.Count:N0}");
		Console.WriteLine($"⏱️ Time Taken: {sw.Elapsed.TotalMilliseconds:F2} ms");
		Console.WriteLine($"📊 Throughput: {ids.Count / sw.Elapsed.TotalSeconds:F0} tokens/sec");
		
		// Memory usage
		var memory = GC.GetTotalMemory(false) / 1024 / 1024;
		Console.WriteLine($"💾 Memory: {memory:F1} MB");
	}

	static async Task RunParallelBenchmark(string filePath, string vocabPath)
	{
		string text = File.ReadAllText(filePath);
		Console.WriteLine($"📄 File: {Path.GetFileName(filePath)} ({text.Length:N0} chars)");
		Console.WriteLine("🚀 Testing: Simple Parallel Tokenizer");

		// Test parallel tokenizer
		var tokenizer = new FlashBertTokenizerSimpleParallel(vocabPath, doLowerCase: true, modelMaxLength: -1, 
			tokenizeChineseChars: true, maxDegreeOfParallelism: Environment.ProcessorCount, chunkSize: 256 * 1024);

		// Warmup
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
		var warmupIds = tokenizer.Encode(text.Substring(0, Math.Min(1000, text.Length)));

		// Benchmark
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
		var sw = Stopwatch.StartNew();
		var ids = tokenizer.Encode(text, padding: "longest", maxLength: -1);
		sw.Stop();

		Console.WriteLine($"🧮 Tokens: {ids.Count:N0}");
		Console.WriteLine($"⏱️ Time Taken: {sw.Elapsed.TotalMilliseconds:F2} ms");
		Console.WriteLine($"📊 Throughput: {ids.Count / sw.Elapsed.TotalSeconds:F0} tokens/sec");
		
		// Memory usage
		var memory = GC.GetTotalMemory(false) / 1024 / 1024;
		Console.WriteLine($"💾 Memory: {memory:F1} MB");
		Console.WriteLine($"🔧 CPU Cores: {Environment.ProcessorCount}");

		// No dispose needed for simple parallel tokenizer
	}

	static async Task RunAsyncBenchmark(string filePath, string vocabPath)
	{
		Console.WriteLine($"📄 File: {Path.GetFileName(filePath)}");
		Console.WriteLine("⚡ Testing: Async Streaming Tokenizer");

		// Warmup with a short-lived pipeline instance (pipeline is single-use)
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
		var warmupText = File.ReadAllText(filePath).Substring(0, Math.Min(1000, File.ReadAllText(filePath).Length));
		using (var warmup = new AsyncTokenizerPipeline(vocabPath, doLowerCase: true, modelMaxLength: -1,
			tokenizeChineseChars: true, maxDegreeOfParallelism: Environment.ProcessorCount,
			chunkSize: 128 * 1024, bufferSize: 1024 * 1024))
		{
			var warmupIds = await warmup.ProcessTextAsync(warmupText);
		}

		// Benchmark with a fresh pipeline instance
		GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
		var sw = Stopwatch.StartNew();
		using var pipeline = new AsyncTokenizerPipeline(vocabPath, doLowerCase: true, modelMaxLength: -1,
			tokenizeChineseChars: true, maxDegreeOfParallelism: Environment.ProcessorCount,
			chunkSize: 128 * 1024, bufferSize: 1024 * 1024);
		var ids = await pipeline.ProcessFileAsync(filePath);
		sw.Stop();

		Console.WriteLine($"🧮 Tokens: {ids.Count:N0}");
		Console.WriteLine($"⏱️ Time Taken: {sw.Elapsed.TotalMilliseconds:F2} ms");
		Console.WriteLine($"📊 Throughput: {ids.Count / sw.Elapsed.TotalSeconds:F0} tokens/sec");
		
		// Memory usage
		var memory = GC.GetTotalMemory(false) / 1024 / 1024;
		Console.WriteLine($"💾 Memory: {memory:F1} MB");
		Console.WriteLine($"🔧 CPU Cores: {Environment.ProcessorCount}");
	}

	static async Task RunComparisonBenchmark(string filePath, string vocabPath)
	{
		string text = File.ReadAllText(filePath);
		Console.WriteLine($"📄 File: {Path.GetFileName(filePath)} ({text.Length:N0} chars)");
		Console.WriteLine("🏁 Running: Complete Performance Comparison\n");

		// 1. Original Optimized
		Console.WriteLine("1️⃣ Optimized Sequential:");
		RunOptimizedBenchmark(filePath, vocabPath);
		Console.WriteLine();

		// 2. Parallel
		Console.WriteLine("2️⃣ Simple Parallel:");
		await RunParallelBenchmark(filePath, vocabPath);
		Console.WriteLine();

		// 3. Async
		Console.WriteLine("3️⃣ Async Streaming:");
		await RunAsyncBenchmark(filePath, vocabPath);
		Console.WriteLine();

		Console.WriteLine("✅ Performance comparison complete!");
	}
}
