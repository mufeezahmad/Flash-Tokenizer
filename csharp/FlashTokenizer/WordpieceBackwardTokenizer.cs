using System.Text;

namespace FlashTokenizer;

public sealed class WordpieceBackwardTokenizer : WordpieceTokenizer
{
	private readonly ACTrie _initialTrie;
	private readonly ACTrie _suffixTrie;
	private readonly Vocab _vocab;
	private readonly string _unk;
	private readonly int _unkId;

	public WordpieceBackwardTokenizer(Vocab vocab, ACTrie initialTrie, ACTrie suffixTrie, string unk = "[UNK]") : base(vocab, unk)
	{
		_vocab = vocab;
		_initialTrie = initialTrie;
		_suffixTrie = suffixTrie;
		_unk = unk;
		_unkId = vocab.Get(unk);
	}

	public int TokenizerIdsBackward(string token, int maxLength, List<int> inputIds)
	{
		var bytes = Encoding.UTF8.GetBytes(token);
		if (bytes.Length > maxLength && inputIds.Count < maxLength)
		{
			inputIds.Add(_unkId);
			return inputIds.Count;
		}
		int originalSize = inputIds.Count;
		int pos = bytes.Length;
		var temp = new List<int>(8);
		while (pos > 0)
		{
			bool found = false;
			int newPos = pos;
			for (int i = 0; i < pos; i++)
			{
				int len = pos - i;
				if (i == 0)
				{
					var span = bytes.AsSpan(0, pos);
					var (mLen, mIdx) = _initialTrie.Search(span, 0);
					if (mIdx != -1 && mLen == span.Length)
					{
						found = true; temp.Add(mIdx); newPos = i; break;
					}
				}
				else
				{
					var span = bytes.AsSpan(i, len);
					var (mLen, mIdx) = _suffixTrie.Search(span, 0);
					if (mIdx != -1 && mLen == span.Length)
					{
						found = true; temp.Add(mIdx); newPos = i; break;
					}
				}
			}
			if (!found)
			{
				inputIds.RemoveRange(originalSize, inputIds.Count - originalSize);
				if (inputIds.Count < maxLength) inputIds.Add(_unkId);
				return inputIds.Count;
			}
			pos = newPos;
		}
		int space = Math.Max(0, maxLength - inputIds.Count);
		int take = Math.Min(space, temp.Count);
		for (int k = take - 1; k >= 0; k--) inputIds.Add(temp[k]);
		return inputIds.Count;
	}
}


