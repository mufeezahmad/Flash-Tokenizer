using System.Runtime.CompilerServices;

using System.Buffers;
using System.Text;

namespace FlashTokenizer;

/// <summary>
/// Basic text preprocessing for BERT: cleaning, whitespace splitting, Chinese char spacing, punctuation split.
/// </summary>
public sealed class BasicTokenizer
{
	private readonly bool _doLowerCase;
	private readonly bool _tokenizeChineseChars;

	public BasicTokenizer(bool doLowerCase = true, bool tokenizeChineseChars = true)
	{
		_doLowerCase = doLowerCase;
		_tokenizeChineseChars = tokenizeChineseChars;
	}

	public List<string> Tokenize(string text)
	{
		var cleaned = _tokenizeChineseChars ? CleanAndTokenize(text) : CleanText(text);
		ReadOnlySpan<char> span = cleaned.AsSpan();
		var tokens = WhitespaceTokenize(span);
		var output = new List<string>(tokens.Count * 2);
		foreach (var token in tokens)
		{
			SplitOnPunc(token, output, _doLowerCase);
		}
		return output;
	}

	public void TokenizeEarlyStop(string text, WordpieceTokenizer wordpiece, int maxLength, List<int> inputIds, int allowedLength)
	{
		var cleaned = _tokenizeChineseChars ? CleanAndTokenize(text) : CleanText(text);
		var tokens = WhitespaceTokenize(cleaned.AsSpan());
		foreach (var token in tokens)
		{
			var splits = new List<string>(4);
			SplitOnPunc(token, splits, _doLowerCase);
			foreach (var t in splits)
			{
				if (wordpiece.TokenizerIds(t, maxLength - 1, inputIds) == allowedLength)
					break;
			}
		}
	}

	private static void WriteBytes(ref System.Buffers.ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> data)
	{
		var span = writer.GetSpan(data.Length);
		data.CopyTo(span);
		writer.Advance(data.Length);
	}

	private static string CleanText(string text)
	{
		var utf8 = Encoding.UTF8.GetBytes(text);
		var builder = new ArrayBufferWriter<byte>(utf8.Length);
		for (int i = 0; i < utf8.Length;)
		{
			int cp = Utf8Util.DecodeCodePoint(utf8, i);
			int len = Utf8Util.CharLen(utf8[i]);
			if (cp == 0 || cp == 0xfffd || cp == 0x2028 || cp == 0x2029 || CharMaps.IsControl(cp))
			{
				i += len; continue;
			}
			if (!CharMaps.IsWhitespace(cp))
			{
				WriteBytes(ref builder, utf8.AsSpan(i, len));
			}
			else
			{
				builder.GetSpan(1)[0] = (byte)' ';
				builder.Advance(1);
			}
			i += len;
		}
		return Encoding.UTF8.GetString(builder.WrittenSpan);
	}

	private static string CleanAndTokenize(string text)
	{
		var utf8 = Encoding.UTF8.GetBytes(text);
		var builder = new ArrayBufferWriter<byte>(utf8.Length * 2);
		ReadOnlySpan<byte> space = stackalloc byte[] { (byte)' ' };
		for (int i = 0; i < utf8.Length;)
		{
			int cp = Utf8Util.DecodeCodePoint(utf8, i);
			int len = Utf8Util.CharLen(utf8[i]);
			if (cp == 0 || cp == 0xfffd || cp == 0x2028 || cp == 0x2029 || CharMaps.IsControl(cp))
			{ i += len; continue; }
			if (CharMaps.IsWhitespace(cp))
			{
				WriteBytes(ref builder, space);
			}
			else if (CharMaps.IsChinese(cp))
			{
				WriteBytes(ref builder, space);
				WriteBytes(ref builder, utf8.AsSpan(i, len));
				WriteBytes(ref builder, space);
			}
			else
			{
				WriteBytes(ref builder, utf8.AsSpan(i, len));
			}
			i += len;
		}
		return Encoding.UTF8.GetString(builder.WrittenSpan);
	}

	private static List<string> WhitespaceTokenize(ReadOnlySpan<char> text)
	{
		var list = new List<string>(Math.Max(1, text.Length / 8));
		int start = -1;
		for (int i = 0; i < text.Length; i++)
		{
			char ch = text[i];
			bool isWs = ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n';
			if (!isWs)
			{
				if (start < 0) start = i;
			}
			else if (start >= 0)
			{
				list.Add(text.Slice(start, i - start).ToString());
				start = -1;
			}
		}
		if (start >= 0) list.Add(text.Slice(start).ToString());
		return list;
	}

	private static void SplitOnPunc(string token, List<string> output, bool toLower)
	{
		if (toLower)
		{
			// Use AccentMap for high-fidelity lower/strip as in C++
			token = AccentMap.ToLowerAndStripAccents(token);
		}
		ReadOnlySpan<byte> bytes = System.Text.Encoding.UTF8.GetBytes(token);
		bool inWord = false; int wordStart = 0; bool hasPunc = false;
		for (int i = 0; i < bytes.Length;)
		{
			int cp = Utf8Util.DecodeCodePoint(bytes, i);
			int len = Utf8Util.CharLen(bytes[i]);
			if (CharMaps.IsPunctuation(cp))
			{
				hasPunc = true;
				if (inWord)
				{
					var slice = bytes.Slice(wordStart, i - wordStart).ToArray();
					var s = System.Text.Encoding.UTF8.GetString(slice);
					output.Add(s);
					inWord = false;
				}
				output.Add(System.Text.Encoding.UTF8.GetString(bytes.Slice(i, len)));
			}
			else if (!inWord)
			{
				wordStart = i;
				inWord = true;
			}
			i += len;
		}
		if (!hasPunc)
		{
			output.Add(token);
			return;
		}
		if (inWord)
		{
			var s = System.Text.Encoding.UTF8.GetString(bytes.Slice(wordStart));
			output.Add(s);
		}
	}
}


