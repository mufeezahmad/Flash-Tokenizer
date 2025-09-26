namespace FlashTokenizer;

public sealed class FlashBertTokenizerBidirectional : FlashBertTokenizer
{
	private readonly Vocab _vocab;
	private readonly BasicTokenizer _basic;
	private readonly WordpieceTokenizer _forward;
	private readonly WordpieceBackwardTokenizer _backward;
	private readonly int _modelMaxLength;
	private readonly int CLS_NUM;
	private readonly int SEP_NUM;

	public FlashBertTokenizerBidirectional(string vocabFile, bool doLowerCase = true, int modelMaxLength = -1, bool tokenizeChineseChars = true)
		: base(vocabFile, doLowerCase, modelMaxLength, tokenizeChineseChars)
	{
		// Build local components for bidirectional processing
		_vocab = new Vocab(vocabFile);
		_basic = new BasicTokenizer(doLowerCase, tokenizeChineseChars);
		_forward = new WordpieceTokenizer(_vocab);
		_modelMaxLength = modelMaxLength;

		var tok2idx = new List<KeyValuePair<string,int>>();
		int idx = 0;
		foreach (var line in File.ReadLines(vocabFile))
		{
			var t = line.TrimEnd();
			if (t.Length == 0) continue;
			tok2idx.Add(new KeyValuePair<string, int>(t, idx++));
		}
		_forward.BuildTries(tok2idx);

		// Build tries and backward tokenizer
		var initialTrie = new ACTrie();
		var suffixTrie = new ACTrie();
		const string suffixIndicator = "##";
		foreach (var kv in tok2idx)
		{
			if (kv.Key.StartsWith(suffixIndicator, StringComparison.Ordinal))
				suffixTrie.Insert(kv.Key.Substring(suffixIndicator.Length), kv.Value);
			else
				initialTrie.Insert(kv.Key, kv.Value);
		}
		initialTrie.Build();
		suffixTrie.Build();
		_backward = new WordpieceBackwardTokenizer(_vocab, initialTrie, suffixTrie);

		CLS_NUM = _vocab.Get("[CLS]");
		SEP_NUM = _vocab.Get("[SEP]");
	}

	public new List<int> Encode(string text, string padding = "max_length", int maxLength = -1)
	{
		if (maxLength == -1) maxLength = _modelMaxLength;
		int effectiveMax = maxLength;
		if (effectiveMax == -1) effectiveMax = int.MaxValue;
		int allowed = effectiveMax - 1;

		var input = new List<int>(Math.Min(1024, effectiveMax));
		input.Add(CLS_NUM);

		var tokens = _basic.Tokenize(text);
		foreach (var tok in tokens)
		{
			var i0 = new List<int>(8);
			var i1 = new List<int>(8);
			_forward.TokenizerIds(tok, effectiveMax - 1, i0);
			_backward.TokenizerIdsBackward(tok, effectiveMax - 1, i1);
			if (ListsEqual(i0, i1))
			{
				input.AddRange(i0);
			}
			else
			{
				var f0 = FilterIds(i0);
				var f1 = FilterIds(i1);
				var best = Heuristics.CompareIds(f0, f1) ? i0 : i1;
				input.AddRange(best);
			}
			if (input.Count > allowed)
			{
				input.RemoveRange(allowed, input.Count - allowed);
				break;
			}
		}

		input.Add(SEP_NUM);
		if (padding == "max_length" && effectiveMax != int.MaxValue)
		{
			while (input.Count < effectiveMax) input.Add(0);
		}
		return input;
	}

	private static bool ListsEqual(List<int> a, List<int> b)
	{
		if (a.Count != b.Count) return false;
		for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
		return true;
	}

	private static List<int> FilterIds(List<int> src)
	{
		var dst = new List<int>(src.Count);
		for (int i = 0; i < src.Count; i++) if (src[i] >= 4) dst.Add(src[i]);
		return dst;
	}
}


