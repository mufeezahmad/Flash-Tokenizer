using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers;

namespace FlashTokenizer;

/// <summary>
/// WordPiece tokenizer core using initial and suffix tries for fast matching.
/// </summary>
public class WordpieceTokenizer
{
	private readonly Vocab _vocab;
	private readonly string _unk;
	private readonly int _unkId;
	private readonly string _suffixIndicator = "##";
	private readonly ACTrie _initialTrie = new();
	private readonly ACTrie _suffixTrie = new();

	public WordpieceTokenizer(Vocab vocab, string unk = "[UNK]")
	{
		_vocab = vocab;
		_unk = unk;
		_unkId = vocab.Get(unk);
	}

	public void BuildTries(IEnumerable<KeyValuePair<string,int>> tokenToIndex)
	{
		foreach (var kv in tokenToIndex)
		{
			var word = kv.Key;
			var idx = kv.Value;
			if (word.StartsWith(_suffixIndicator, StringComparison.Ordinal))
			{
				_suffixTrie.Insert(word.AsSpan(_suffixIndicator.Length).ToString(), idx);
			}
			else
			{
				_initialTrie.Insert(word, idx);
			}
		}
		_initialTrie.Build();
		_suffixTrie.Build();
	}

	/// <summary>
	/// Zero-copy UTF-8 path: consumes a UTF-8 byte span directly.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int TokenizerIdsUtf8(ReadOnlySpan<byte> bytes, int maxLength, List<int> inputIds)
	{
		if (bytes.Length > maxLength && inputIds.Count < maxLength)
		{
			inputIds.Add(_unkId);
			return inputIds.Count;
		}

		int originalSize = inputIds.Count;
		int start = 0;
		while (start < bytes.Length)
		{
			var trie = start != 0 ? _suffixTrie : _initialTrie;
			var (len, idx) = trie.Search(bytes, start);
			if (idx == -1)
			{
				inputIds.RemoveRange(originalSize, inputIds.Count - originalSize);
				if (inputIds.Count < maxLength) inputIds.Add(_unkId);
				return inputIds.Count;
			}
			if (inputIds.Count < maxLength) inputIds.Add(idx); else return inputIds.Count;
			start += len;
		}
		return inputIds.Count;
	}

	public int TokenizerIds(string token, int maxLength, List<int> inputIds)
	{
		// Use ArrayPool to reduce per-token allocations
		var encoding = Encoding.UTF8;
		int maxByteCount = encoding.GetMaxByteCount(token.Length);
		byte[] rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
		try
		{
			int byteLen = encoding.GetBytes(token, 0, token.Length, rented, 0);
			var bytes = rented.AsSpan(0, byteLen);
			if (bytes.Length > maxLength && inputIds.Count < maxLength)
			{
				inputIds.Add(_unkId);
				return inputIds.Count;
			}
			int originalSize = inputIds.Count;
			int start = 0;
			while (start < bytes.Length)
			{
				var trie = start != 0 ? _suffixTrie : _initialTrie;
				var (len, idx) = trie.Search(bytes, start);
				if (idx == -1)
				{
					inputIds.RemoveRange(originalSize, inputIds.Count - originalSize);
					if (inputIds.Count < maxLength) inputIds.Add(_unkId);
					return inputIds.Count;
				}
				if (inputIds.Count < maxLength) inputIds.Add(idx); else return inputIds.Count;
				start += len;
			}
			return inputIds.Count;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(rented);
		}
	}
}


