using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FlashTokenizer;

public sealed class Vocab
{
	private readonly Dictionary<string,int> _tokenToIndex = new(StringComparer.Ordinal);
	private readonly Dictionary<int,string> _indexToToken = new();

	public Vocab(string vocabFile)
	{
		using var reader = new StreamReader(vocabFile);
		string? line;
		int idx = 0;
		while ((line = reader.ReadLine()) != null)
		{
			line = line.TrimEnd();
			if (line.Length == 0) continue;
			_indexToToken[idx] = line;
			_tokenToIndex[line] = idx;
			idx++;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int Get(string token, int defaultValue = 0)
		=> _tokenToIndex.TryGetValue(token, out var id) ? id : defaultValue;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string Get(int id) => _indexToToken[id];
}


