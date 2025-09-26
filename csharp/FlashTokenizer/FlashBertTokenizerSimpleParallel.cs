using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FlashTokenizer;

/// <summary>
/// Simple parallel BERT WordPiece tokenizer with basic chunking.
/// </summary>
public class FlashBertTokenizerSimpleParallel : ITokenizer
{
	private readonly Vocab _vocab;
	private readonly BasicTokenizerOptimized _basic;
	private readonly WordpieceTokenizer _wordpiece;
	private readonly int _modelMaxLength;
	private readonly int _maxDegreeOfParallelism;
	private readonly int _chunkSize;

	private readonly string UNK = "[UNK]";
	private readonly string CLS = "[CLS]";
	private readonly string SEP = "[SEP]";
	private readonly int CLS_NUM;
	private readonly int SEP_NUM;
	private readonly int UNK_NUM;

	public FlashBertTokenizerSimpleParallel(string vocabFile, bool doLowerCase = true, int modelMaxLength = -1, 
		bool tokenizeChineseChars = true, int maxDegreeOfParallelism = -1, int chunkSize = 256 * 1024)
	{
		_vocab = new Vocab(vocabFile);
		_basic = new BasicTokenizerOptimized(doLowerCase, tokenizeChineseChars);
		_wordpiece = new WordpieceTokenizer(_vocab);
		_modelMaxLength = modelMaxLength;
		_maxDegreeOfParallelism = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;
		_chunkSize = chunkSize;

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

		CLS_NUM = _vocab.Get(CLS);
		SEP_NUM = _vocab.Get(SEP);
		UNK_NUM = _vocab.Get(UNK);
	}

	/// <summary>
	/// Encode input text to token ids with parallel processing for large documents.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public List<int> Encode(string text, string padding = "max_length", int maxLength = -1)
	{
		if (maxLength == -1) maxLength = _modelMaxLength;
		
		// For small documents, use sequential processing
		if (text.Length < _chunkSize)
		{
			return EncodeSequential(text, maxLength, padding);
		}

		// For large documents, use parallel processing
		return EncodeParallel(text, maxLength, padding);
	}

	// ITokenizer compatibility overload
	public List<int> Encode(string input) => Encode(input, "max_length", -1);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private List<int> EncodeSequential(string text, int maxLength, string padding)
	{
		int effectiveMax = maxLength == -1 ? _modelMaxLength : maxLength;
		if (effectiveMax == -1) effectiveMax = int.MaxValue;
		int allowed = effectiveMax - 1;
		
		var input = new List<int>(Math.Min(1024, effectiveMax));
		input.Add(CLS_NUM);
		
		_basic.TokenizeEarlyStop(text, _wordpiece, effectiveMax, input, allowed);
		
		input.Add(SEP_NUM);
		if (padding == "max_length" && effectiveMax != int.MaxValue)
		{
			while (input.Count < effectiveMax) input.Add(0);
		}
		return input;
	}

	private List<int> EncodeParallel(string text, int maxLength, string padding)
	{
		int effectiveMax = maxLength == -1 ? _modelMaxLength : maxLength;
		if (effectiveMax == -1) effectiveMax = int.MaxValue;

		// Simple chunking by fixed size
		var chunks = new List<string>();
		for (int i = 0; i < text.Length; i += _chunkSize)
		{
			int length = Math.Min(_chunkSize, text.Length - i);
			chunks.Add(text.Substring(i, length));
		}

		// Process chunks in parallel
		var chunkResults = new ConcurrentBag<List<int>>();
		var parallelOptions = new ParallelOptions
		{
			MaxDegreeOfParallelism = _maxDegreeOfParallelism
		};

		Parallel.ForEach(chunks, parallelOptions, chunk =>
		{
			var chunkTokens = ProcessChunk(chunk);
			chunkResults.Add(chunkTokens);
		});

		// Merge results
		var finalResult = new List<int>();
		finalResult.Add(CLS_NUM);

		foreach (var chunkResult in chunkResults)
		{
			finalResult.AddRange(chunkResult);
		}

		finalResult.Add(SEP_NUM);

		// Apply length limits
		if (finalResult.Count > effectiveMax)
		{
			finalResult.RemoveRange(effectiveMax, finalResult.Count - effectiveMax);
		}

		// Apply padding
		if (padding == "max_length" && effectiveMax != int.MaxValue)
		{
			while (finalResult.Count < effectiveMax)
			{
				finalResult.Add(0);
			}
		}

		return finalResult;
	}

	private List<int> ProcessChunk(string chunk)
	{
		var inputIds = new List<int>();
		
		// Create a new BasicTokenizer for this thread to avoid contention
		var basicTokenizer = new BasicTokenizerOptimized(_basic.DoLowerCase, _basic.TokenizeChineseChars);
		basicTokenizer.TokenizeEarlyStop(chunk, _wordpiece, int.MaxValue, inputIds, int.MaxValue);
		
		return inputIds;
	}

	/// <summary>
	/// Decode token ids to a readable string (merging WordPiece prefixes).
	/// </summary>
	public string Decode(IEnumerable<int> tokenIds)
	{
		var sb = new System.Text.StringBuilder();
		bool first = true;
		foreach (var id in tokenIds)
		{
			if (id == 0) continue; // pad
			var t = _vocab.Get(id);
			if (t == CLS || t == SEP) continue;
			if (t.StartsWith("##", StringComparison.Ordinal))
			{
				sb.Append(t.AsSpan(2));
			}
			else
			{
				if (!first) sb.Append(' ');
				sb.Append(t);
				first = false;
			}
		}
		return sb.ToString();
	}
}
