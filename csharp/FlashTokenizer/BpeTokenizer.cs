using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlashTokenizer;

/// <summary>
/// GPT-2 style Byte Pair Encoding (byte-level) tokenizer with regex pre-tokenization.
/// </summary>
public sealed class BpeTokenizer : ITokenizer
{
	private readonly Dictionary<string,int> _vocab;
	private readonly Dictionary<(string a,string b), int> _ranks;
	private readonly string[] _idToToken;
	private readonly Dictionary<string, string[]> _bpeCache = new(StringComparer.Ordinal);

	// GPT-2 byte encoder/decoder tables
	private static readonly Dictionary<byte, char> ByteEncoder = BuildByteEncoder();
	private static readonly Dictionary<char, byte> ByteDecoder = BuildByteDecoder(ByteEncoder);

	// Canonical GPT-2 regex pattern (from encoder.py)
	private static readonly Regex Gpt2Pattern = new(
		"'s|'t|'re|'ve|'m|'ll|'d| ?\\p{L}+| ?\\p{N}+| ?[^\\s\\p{L}\\p{N}]+|\\s+(?!\\S)|\\s+",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	/// Create BPE tokenizer from vocab.json and merges.txt.
	/// </summary>
	public BpeTokenizer(string vocabJsonPath, string mergesPath)
	{
		// Load vocab.json (token -> id)
		using var vfs = File.OpenRead(vocabJsonPath);
		var vocabDoc = JsonDocument.Parse(vfs);
		_vocab = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var prop in vocabDoc.RootElement.EnumerateObject())
		{
			_vocab[prop.Name] = prop.Value.GetInt32();
		}
		_idToToken = new string[_vocab.Count];
		foreach (var kv in _vocab) _idToToken[kv.Value] = kv.Key;

		// Load merges.txt
		_ranks = new Dictionary<(string a, string b), int>();
		var lines = File.ReadAllLines(mergesPath);
		int rank = 0;
		foreach (var raw in lines)
		{
			var line = raw.Trim();
			if (line.Length == 0 || line.StartsWith("#")) continue;
			var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 2)
			{
				_ranks[(parts[0], parts[1])] = rank++;
			}
		}
	}

	/// <inheritdoc />
	public List<int> Encode(string input)
	{
		// Byte-level encode to Unicode string using GPT-2 byte encoder
		var encoded = ByteEncodeToUnicode(input);
		var matches = Gpt2Pattern.Matches(encoded);
		var result = new List<int>(matches.Count * 2);
		foreach (Match m in matches)
		{
			var piece = m.Value;
			foreach (var bpeTok in ApplyBpe(piece))
			{
				if (_vocab.TryGetValue(bpeTok, out var id)) result.Add(id);
			}
		}
		return result;
	}

	/// <inheritdoc />
	public string Decode(IEnumerable<int> ids)
	{
		// Reconstruct the encoded Unicode string from tokens then byte-decode
		var builder = new StringBuilder();
		foreach (var id in ids)
		{
			if (id >= 0 && id < _idToToken.Length) builder.Append(_idToToken[id]);
		}
		return ByteDecodeFromUnicode(builder.ToString());
	}

	private IEnumerable<string> ApplyBpe(string token)
	{
		if (_bpeCache.TryGetValue(token, out var cached)) return cached;

		if (token.Length == 0)
		{
			_bpeCache[token] = Array.Empty<string>();
			return _bpeCache[token];
		}

		// Represent word as ranges over the single backing string
		var ranges = new List<(int start,int len)>(token.Length);
		for (int i = 0; i < token.Length; i++) ranges.Add((i, 1));

		while (true)
		{
			if (ranges.Count == 1) break;
			var pairs = GetPairsRanges(token, ranges);
			int bestIndex = -1;
			int bestRank = int.MaxValue;
			for (int i = 0; i < pairs.Count; i++)
			{
				var (a, b) = pairs[i];
				if (_ranks.TryGetValue((a, b), out var r) && r < bestRank)
				{
					bestRank = r;
					bestIndex = i;
				}
			}
			if (bestIndex == -1) break;

			// Merge first occurrence of best pair
			var merged = new List<(int start,int len)>(ranges.Count);
			int pos = 0;
			while (pos < ranges.Count)
			{
				int j = IndexOfAdjacentRanges(token, ranges, pairs[bestIndex].a, pairs[bestIndex].b, pos);
				if (j == -1)
				{
					for (int k = pos; k < ranges.Count; k++) merged.Add(ranges[k]);
					break;
				}
				for (int k = pos; k < j; k++) merged.Add(ranges[k]);
				merged.Add((ranges[j].start, ranges[j].len + ranges[j + 1].len));
				pos = j + 2;
			}
			ranges = merged;
		}

		var outTokens = new string[ranges.Count];
		for (int i = 0; i < ranges.Count; i++) outTokens[i] = token.Substring(ranges[i].start, ranges[i].len);
		_bpeCache[token] = outTokens;
		return outTokens;
	}

	private static List<(string a, string b)> GetPairsRanges(string token, List<(int start,int len)> ranges)
	{
		var pairs = new List<(string a, string b)>(Math.Max(0, ranges.Count - 1));
		for (int i = 0; i + 1 < ranges.Count; i++)
		{
			pairs.Add((token.Substring(ranges[i].start, ranges[i].len), token.Substring(ranges[i+1].start, ranges[i+1].len)));
		}
		return pairs;
	}

	private static int IndexOfAdjacentRanges(string token, List<(int start,int len)> ranges, string a, string b, int start)
	{
		for (int i = start; i + 1 < ranges.Count; i++)
		{
			if (token.AsSpan(ranges[i].start, ranges[i].len).SequenceEqual(a) && token.AsSpan(ranges[i+1].start, ranges[i+1].len).SequenceEqual(b)) return i;
		}
		return -1;
	}

	private static string ByteEncodeToUnicode(string s)
	{
		ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(s);
		var builder = new StringBuilder(bytes.Length);
		for (int i = 0; i < bytes.Length; i++) builder.Append(ByteEncoder[bytes[i]]);
		return builder.ToString();
	}

	private static string ByteDecodeFromUnicode(string s)
	{
		var bytes = new byte[s.Length];
		int count = 0;
		for (int i = 0; i < s.Length; i++)
		{
			if (ByteDecoder.TryGetValue(s[i], out var b)) bytes[count++] = b;
		}
		return Encoding.UTF8.GetString(bytes, 0, count);
	}

	private static Dictionary<byte, char> BuildByteEncoder()
	{
		// Canonical GPT-2 bytes->unicode mapping
		var bs = new List<int>();
		var cs = new List<int>();
		for (int b = (int)'!'; b <= (int)'~'; b++) { bs.Add(b); cs.Add(b); }
		for (int b = 0xA1; b <= 0xAC; b++) { bs.Add(b); cs.Add(b); }
		for (int b = 0xAE; b <= 0xFF; b++) { bs.Add(b); cs.Add(b); }
		int n = 0;
		for (int b = 0; b < 256; b++) if (!bs.Contains(b)) { bs.Add(b); cs.Add(256 + n); n++; }
		var dict = new Dictionary<byte, char>(256);
		for (int i = 0; i < bs.Count; i++) dict[(byte)bs[i]] = (char)cs[i];
		return dict;
	}

	private static Dictionary<char, byte> BuildByteDecoder(Dictionary<byte, char> enc)
	{
		var dec = new Dictionary<char, byte>(enc.Count);
		foreach (var kv in enc) dec[kv.Value] = kv.Key;
		return dec;
	}
}


