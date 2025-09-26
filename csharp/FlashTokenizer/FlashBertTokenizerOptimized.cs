using System.Runtime.CompilerServices;

namespace FlashTokenizer;

/// <summary>
/// Ultra-high-performance BERT WordPiece tokenizer with optimized BasicTokenizer.
/// </summary>
public class FlashBertTokenizerOptimized : ITokenizer
{
	private readonly Vocab _vocab;
	private readonly BasicTokenizerOptimized _basic;
	private readonly WordpieceTokenizer _wordpiece;
	private readonly int _modelMaxLength;

	private readonly string UNK = "[UNK]";
	private readonly string CLS = "[CLS]";
	private readonly string SEP = "[SEP]";
	private readonly int CLS_NUM;
	private readonly int SEP_NUM;
	private readonly int UNK_NUM;

	public FlashBertTokenizerOptimized(string vocabFile, bool doLowerCase = true, int modelMaxLength = -1, bool tokenizeChineseChars = true)
	{
		_vocab = new Vocab(vocabFile);
		_basic = new BasicTokenizerOptimized(doLowerCase, tokenizeChineseChars);
		_wordpiece = new WordpieceTokenizer(_vocab);
		_modelMaxLength = modelMaxLength;

		// Build tries from vocab content - optimized to avoid LINQ
		var tok2idx = new List<KeyValuePair<string,int>>();
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
	/// Encode input text to token ids with optional padding and max length.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public List<int> Encode(string text, string padding = "max_length", int maxLength = -1)
	{
		if (maxLength == -1) maxLength = _modelMaxLength;
		return TokenizerIds(text, maxLength, padding);
	}

	// ITokenizer compatibility overload
	public List<int> Encode(string input) => Encode(input, "max_length", -1);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private List<int> TokenizerIds(string text, int maxLength, string padding)
	{
		int effectiveMax = maxLength == -1 ? _modelMaxLength : maxLength;
		if (effectiveMax == -1) effectiveMax = int.MaxValue;
		int allowed = effectiveMax - 1;
		using var session = new TokenizerSession();
		var input = session.InputIds;
		input.Add(CLS_NUM);
		// Use optimized BasicTokenizer with pooled session buffers
		_basic.TokenizeEarlyStop(text, _wordpiece, effectiveMax, input, allowed);
		input.Add(SEP_NUM);
		if (padding == "max_length" && effectiveMax != int.MaxValue)
		{
			while (input.Count < effectiveMax) input.Add(0);
		}
		return input;
	}

	/// <summary>
	/// Decode token ids to a readable string (merging WordPiece prefixes).
	/// </summary>
	public string Decode(IEnumerable<int> tokenIds)
	{
		// Simple decode: remove [CLS],[SEP],0 padding and merge WordPiece "##" prefixes
		var sb = new System.Text.StringBuilder();
		bool first = true;
		foreach (var id in tokenIds)
		{
			if (id == 0) continue; // pad
			var t = _vocab.Get(id);
			if (t == CLS || t == SEP) continue;
			if (t.StartsWith("##", System.StringComparison.Ordinal))
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
