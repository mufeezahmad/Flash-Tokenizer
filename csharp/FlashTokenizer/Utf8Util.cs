using System.Runtime.CompilerServices;

namespace FlashTokenizer;

internal static class Utf8Util
{
	private static readonly byte[] Utf8Len = new byte[256];
	static Utf8Util()
	{
		for (int i = 0; i < 256; i++) Utf8Len[i] = 1;
		for (int i = 0xC2; i <= 0xDF; i++) Utf8Len[i] = 2;
		for (int i = 0xE0; i <= 0xEF; i++) Utf8Len[i] = 3;
		for (int i = 0xF0; i <= 0xF4; i++) Utf8Len[i] = 4;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CharLen(byte first) => Utf8Len[first];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int DecodeCodePoint(ReadOnlySpan<byte> s, int pos)
	{
		if (pos >= s.Length) return 0;
		byte b0 = s[pos];
		if (b0 < 0x80) return b0;
		if ((b0 & 0xE0) == 0xC0)
		{
			if (pos + 1 >= s.Length) return 0;
			byte b1 = s[pos + 1];
			if ((b1 & 0xC0) != 0x80) return 0;
			int cp = ((b0 & 0x1F) << 6) | (b1 & 0x3F);
			return cp < 0x80 ? 0 : cp;
		}
		if ((b0 & 0xF0) == 0xE0)
		{
			if (pos + 2 >= s.Length) return 0;
			byte b1 = s[pos + 1];
			byte b2 = s[pos + 2];
			if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80) return 0;
			int cp = ((b0 & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (b2 & 0x3F);
			return (cp < 0x800 || (cp >= 0xD800 && cp <= 0xDFFF)) ? 0 : cp;
		}
		if ((b0 & 0xF8) == 0xF0)
		{
			if (pos + 3 >= s.Length) return 0;
			byte b1 = s[pos + 1];
			byte b2 = s[pos + 2];
			byte b3 = s[pos + 3];
			if ((b1 & 0xC0) != 0x80 || (b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80) return 0;
			int cp = ((b0 & 0x07) << 18) | ((b1 & 0x3F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
			return (cp < 0x10000 || cp > 0x10FFFF) ? 0 : cp;
		}
		return 0;
	}
}


