namespace FlashTokenizer;

/// <summary>
/// High-performance BERT WordPiece tokenizer (forward direction).
/// </summary>
public class FlashBertTokenizer : ITokenizer
{
	private readonly Vocab _vocab;
	private readonly BasicTokenizer _basic;
	private readonly WordpieceTokenizer _wordpiece;
	private readonly int _modelMaxLength;

	private readonly string UNK = "[UNK]";
	private readonly string CLS = "[CLS]";
	private readonly string SEP = "[SEP]";
	private readonly int CLS_NUM;
	private readonly int SEP_NUM;
	private readonly int UNK_NUM;

	public FlashBertTokenizer(string vocabFile, bool doLowerCase = true, int modelMaxLength = -1, bool tokenizeChineseChars = true)
	{
		_vocab = new Vocab(vocabFile);
		_basic = new BasicTokenizer(doLowerCase, tokenizeChineseChars);
		_wordpiece = new WordpieceTokenizer(_vocab);
		_modelMaxLength = modelMaxLength;

		// Build tries from vocab content
		var tok2idx = new List<KeyValuePair<string,int>>();
		// Reflect vocab internal map using the input file again
		// For performance and simplicity, re-read file to get ordered tokens
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
	/// <param name="text">Input text.</param>
	/// <param name="padding">Padding mode: "max_length" or other.</param>
	/// <param name="maxLength">Max length; -1 uses model default.</param>
	public List<int> Encode(string text, string padding = "max_length", int maxLength = -1)
	{
		if (maxLength == -1) maxLength = _modelMaxLength;
		return TokenizerIds(text, maxLength, padding);
	}

	// ITokenizer compatibility overload
	public List<int> Encode(string input) => Encode(input, "max_length", -1);

	public List<List<int>> BatchEncode(IEnumerable<string> texts, string padding = "max_length", int maxLength = -1, bool parallel = false)
	{
		if (maxLength == -1) maxLength = _modelMaxLength;
		var list = texts.ToList();
		var result = new List<List<int>>(list.Count);
		if (parallel)
		{
			result = list.AsParallel().AsOrdered().Select(t => TokenizerIds(t, maxLength, padding)).ToList();
		}
		else
		{
			foreach (var t in list) result.Add(TokenizerIds(t, maxLength, padding));
		}
		return result;
	}

	public List<string> Tokenize(string text)
	{
		var ids = TokenizerIds(text, int.MaxValue, "longest");
		var tokens = new List<string>(Math.Max(0, ids.Count - 2));
		for (int i = 1; i < ids.Count - 1; i++) tokens.Add(_vocab.Get(ids[i]));
		return tokens;
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

	private List<int> TokenizerIds(string text, int maxLength, string padding)
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
}


