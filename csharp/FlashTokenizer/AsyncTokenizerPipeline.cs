using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace FlashTokenizer;

/// <summary>
/// Async tokenization pipeline for streaming large documents with optimal memory usage.
/// </summary>
public class AsyncTokenizerPipeline : IDisposable
{
	private readonly Vocab _vocab;
	private readonly WordpieceTokenizer _wordpiece;
	private readonly int _modelMaxLength;
	private readonly bool _doLowerCase;
	private readonly bool _tokenizeChineseChars;
	private readonly int _maxDegreeOfParallelism;
	private readonly int _chunkSize;
	private readonly int _bufferSize;

	// Async pipeline components
	private readonly Channel<TextChunk> _inputChannel;
	private readonly Channel<TokenChunk> _outputChannel;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private volatile bool _inputCompleted;
	private volatile bool _outputCompleted;

	public AsyncTokenizerPipeline(string vocabFile, bool doLowerCase = true, int modelMaxLength = -1,
		bool tokenizeChineseChars = true, int maxDegreeOfParallelism = -1, int chunkSize = 128 * 1024,
		int bufferSize = 1024 * 1024)
	{
		_vocab = new Vocab(vocabFile);
		_wordpiece = new WordpieceTokenizer(_vocab);
		_modelMaxLength = modelMaxLength;
		_doLowerCase = doLowerCase;
		_tokenizeChineseChars = tokenizeChineseChars;
		_maxDegreeOfParallelism = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;
		_chunkSize = chunkSize;
		_bufferSize = bufferSize;

		// Build tries from vocab content
		var tok2idx = new List<KeyValuePair<string, int>>();
		int idx = 0;
		foreach (var line in File.ReadLines(vocabFile))
		{
			var t = line.TrimEnd();
			if (t.Length == 0) continue;
			tok2idx.Add(new KeyValuePair<string, int>(t, idx++));
		}
		_wordpiece.BuildTries(tok2idx);

		// Initialize async pipeline
		_inputChannel = Channel.CreateUnbounded<TextChunk>();
		_outputChannel = Channel.CreateUnbounded<TokenChunk>();
		_cancellationTokenSource = new CancellationTokenSource();
	}

	/// <summary>
	/// Process a large file asynchronously with streaming tokenization.
	/// </summary>
	public async Task<List<int>> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default)
	{
		var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;

		// Start the processing pipeline
		var processingTask = StartProcessingPipelineAsync(combinedToken);
		var readingTask = ReadFileAsync(filePath, combinedToken);
		var collectingTask = CollectResultsAsync(combinedToken);

		// Wait for all tasks to complete
		await Task.WhenAll(readingTask, processingTask, collectingTask);

		return collectingTask.Result;
	}

	/// <summary>
	/// Process text asynchronously with streaming.
	/// </summary>
	public async Task<List<int>> ProcessTextAsync(string text, CancellationToken cancellationToken = default)
	{
		var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;

		// Start the processing pipeline
		var processingTask = StartProcessingPipelineAsync(combinedToken);
		var readingTask = ReadTextAsync(text, combinedToken);
		var collectingTask = CollectResultsAsync(combinedToken);

		// Wait for all tasks to complete
		await Task.WhenAll(readingTask, processingTask, collectingTask);

		return collectingTask.Result;
	}

	private async Task ReadFileAsync(string filePath, CancellationToken cancellationToken)
	{
		try
		{
			using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, useAsync: true);
			using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, _bufferSize);

			var buffer = new char[_chunkSize];
			int chunkIndex = 0;

			while (!cancellationToken.IsCancellationRequested)
			{
				int bytesRead = await reader.ReadAsync(buffer, 0, _chunkSize);
				if (bytesRead == 0) break;

				var chunk = new TextChunk
				{
					Text = new string(buffer, 0, bytesRead),
					ChunkIndex = chunkIndex++,
					IsLast = bytesRead < _chunkSize
				};

				await _inputChannel.Writer.WriteAsync(chunk, cancellationToken);
			}

			SafeCompleteInput();
		}
		catch (Exception ex)
		{
			SafeCompleteInput(ex);
			throw;
		}
	}

	private async Task ReadTextAsync(string text, CancellationToken cancellationToken)
	{
		try
		{
			int start = 0;
			int chunkIndex = 0;

			while (start < text.Length && !cancellationToken.IsCancellationRequested)
			{
				int length = Math.Min(_chunkSize, text.Length - start);
				var chunk = new TextChunk
				{
					Text = text.Substring(start, length),
					ChunkIndex = chunkIndex++,
					IsLast = start + length >= text.Length
				};

				await _inputChannel.Writer.WriteAsync(chunk, cancellationToken);
				start += length;
			}

			SafeCompleteInput();
		}
		catch (Exception ex)
		{
			SafeCompleteInput(ex);
			throw;
		}
	}

	private async Task StartProcessingPipelineAsync(CancellationToken cancellationToken)
	{
		var semaphore = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);
		var tasks = new List<Task>();

		try
		{
			await foreach (var chunk in _inputChannel.Reader.ReadAllAsync(cancellationToken))
			{
				var task = ProcessChunkAsync(chunk, semaphore, cancellationToken);
				tasks.Add(task);
			}

			await Task.WhenAll(tasks);
			SafeCompleteOutput();
		}
		catch (Exception ex)
		{
			SafeCompleteOutput(ex);
			throw;
		}
		finally
		{
			semaphore.Dispose();
		}
	}

	private async Task ProcessChunkAsync(TextChunk chunk, SemaphoreSlim semaphore, CancellationToken cancellationToken)
	{
		await semaphore.WaitAsync(cancellationToken);
		try
		{
			// Process chunk on thread pool
			await Task.Run(() =>
			{
				var basicTokenizer = new BasicTokenizerOptimized(_doLowerCase, _tokenizeChineseChars);
				var inputIds = new List<int>();
				
				basicTokenizer.TokenizeEarlyStop(chunk.Text, _wordpiece, int.MaxValue, inputIds, int.MaxValue);

				var tokenChunk = new TokenChunk
				{
					Tokens = inputIds,
					ChunkIndex = chunk.ChunkIndex,
					IsLast = chunk.IsLast
				};

				_outputChannel.Writer.TryWrite(tokenChunk);
			}, cancellationToken);
		}
		finally
		{
			semaphore.Release();
		}
	}

	private async Task<List<int>> CollectResultsAsync(CancellationToken cancellationToken)
	{
		var results = new ConcurrentDictionary<int, List<int>>();
		int totalChunks = 0;

		await foreach (var tokenChunk in _outputChannel.Reader.ReadAllAsync(cancellationToken))
		{
			results[tokenChunk.ChunkIndex] = tokenChunk.Tokens;
			if (tokenChunk.IsLast)
			{
				totalChunks = tokenChunk.ChunkIndex + 1;
			}
		}

		// Merge results in order
		var finalResult = new List<int>();
		for (int i = 0; i < totalChunks; i++)
		{
			if (results.TryGetValue(i, out var chunkTokens))
			{
				finalResult.AddRange(chunkTokens);
			}
		}

		return finalResult;
	}

	public void Dispose()
	{
		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource?.Dispose();
		SafeCompleteInput();
		SafeCompleteOutput();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SafeCompleteInput(Exception? error = null)
	{
		if (_inputCompleted) return;
		try
		{
			if (error is null) _inputChannel.Writer.Complete(); else _inputChannel.Writer.Complete(error);
		}
		catch { /* ignore if already completed */ }
		finally { _inputCompleted = true; }
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SafeCompleteOutput(Exception? error = null)
	{
		if (_outputCompleted) return;
		try
		{
			if (error is null) _outputChannel.Writer.Complete(); else _outputChannel.Writer.Complete(error);
		}
		catch { /* ignore if already completed */ }
		finally { _outputCompleted = true; }
	}

	private struct TextChunk
	{
		public string Text;
		public int ChunkIndex;
		public bool IsLast;
	}

	private struct TokenChunk
	{
		public List<int> Tokens;
		public int ChunkIndex;
		public bool IsLast;
	}
}
