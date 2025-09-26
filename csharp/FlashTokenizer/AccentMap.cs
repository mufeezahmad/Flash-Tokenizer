using System.Text;
using System.Buffers;
using System.Reflection;

namespace FlashTokenizer;

internal static class AccentMap
{
	private static readonly Dictionary<int, string> Map = BuildFromUpstreamCharmap();

	private static Dictionary<int, string> BuildFromUpstreamCharmap()
	{
		var result = new Dictionary<int, string>();
		var asm = Assembly.GetExecutingAssembly();
		string? resName = null;
		foreach (var name in asm.GetManifestResourceNames())
		{
			if (name.EndsWith("upstream_charmap.h", StringComparison.Ordinal))
			{
				resName = name; break;
			}
		}
		if (resName is null) return result;
		using var stream = asm.GetManifestResourceStream(resName)!;
		using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 16);
		string? line;
		// Parse lines like: charMap[0x00c0] = codePointToUTF8(0x0061);
		while ((line = reader.ReadLine()) != null)
		{
			line = line.Trim();
			if (!line.StartsWith("charMap[0x", StringComparison.Ordinal)) continue;
			int lb = line.IndexOf('[');
			int rb = line.IndexOf(']');
			int lp = line.IndexOf("0x", rb + 1, StringComparison.Ordinal);
			int rp = line.IndexOf(')', lp + 1);
			if (lb < 0 || rb < 0 || lp < 0 || rp < 0) continue;
			var keyHex = line.AsSpan(lb + 1, rb - lb - 1);
			var valHex = line.AsSpan(lp, rp - lp);
			if (keyHex.StartsWith("0x") && valHex.StartsWith("0x"))
			{
				if (int.TryParse(keyHex[2..].ToString(), System.Globalization.NumberStyles.HexNumber, null, out int key) &&
					int.TryParse(valHex[2..].ToString(), System.Globalization.NumberStyles.HexNumber, null, out int val))
				{
					// Convert code point to UTF-8 string (same as upstream codePointToUTF8)
					var s = CodePointToUtf8(val);
					result[key] = s;
				}
			}
		}
		return result;
	}

	private static string CodePointToUtf8(int codePoint)
	{
		Span<byte> buf = stackalloc byte[4];
		int len = 0;
		if (codePoint < 0x80)
		{
			buf[0] = (byte)codePoint; len = 1;
		}
		else if (codePoint < 0x800)
		{
			buf[0] = (byte)(0xC0 | (codePoint >> 6));
			buf[1] = (byte)(0x80 | (codePoint & 0x3F));
			len = 2;
		}
		else if (codePoint < 0x10000)
		{
			buf[0] = (byte)(0xE0 | (codePoint >> 12));
			buf[1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
			buf[2] = (byte)(0x80 | (codePoint & 0x3F));
			len = 3;
		}
		else
		{
			buf[0] = (byte)(0xF0 | (codePoint >> 18));
			buf[1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
			buf[2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
			buf[3] = (byte)(0x80 | (codePoint & 0x3F));
			len = 4;
		}
		return Encoding.UTF8.GetString(buf[..len]);
	}

	public static bool TryMap(int codePoint, out string replacement)
	{
		return Map.TryGetValue(codePoint, out replacement!);
	}

	public static string ToLowerAndStripAccents(string input)
	{
		// ASCII lower fast path + exact upstream accent replacement + fallback NFKD strip
		ReadOnlySpan<byte> bytes = Encoding.UTF8.GetBytes(input);
		var builder = new ArrayBufferWriter<byte>(bytes.Length * 2);
		for (int i = 0; i < bytes.Length;)
		{
			int cp = Utf8Util.DecodeCodePoint(bytes, i);
			int len = Utf8Util.CharLen(bytes[i]);
			if (cp < 128)
			{
				byte b = bytes[i];
				if (b >= (byte)'A' && b <= (byte)'Z') b = (byte)(b + 32);
				builder.GetSpan(1)[0] = b;
				builder.Advance(1);
			}
			else if (TryMap(cp, out var repl))
			{
				var r = Encoding.UTF8.GetBytes(repl);
				var span = builder.GetSpan(r.Length);
				r.CopyTo(span);
				builder.Advance(r.Length);
			}
			else
			{
				var s = Encoding.UTF8.GetString(bytes.Slice(i, len));
				var nfkd = s.Normalize(NormalizationForm.FormD);
				var filtered = new StringBuilder(nfkd.Length);
				foreach (var ch in nfkd)
				{
					var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
					if (cat != System.Globalization.UnicodeCategory.NonSpacingMark && cat != System.Globalization.UnicodeCategory.SpacingCombiningMark)
						filtered.Append(char.ToLowerInvariant(ch));
				}
				var rb = Encoding.UTF8.GetBytes(filtered.ToString());
				var sp = builder.GetSpan(rb.Length);
				rb.CopyTo(sp);
				builder.Advance(rb.Length);
			}
			i += len;
		}
		return Encoding.UTF8.GetString(builder.WrittenSpan);
	}
}


