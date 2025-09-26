using FlashTokenizer;

namespace FlashTokenizer.Tests;

public static class SmokeTests
{
	// Minimal harness; not a real unit test framework binding to keep it simple
	public static void Run()
	{
		var vocabPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample", "vocab.txt");
		vocabPath = Path.GetFullPath(vocabPath);
		var tok = new FlashBertTokenizer(vocabPath, doLowerCase: true, modelMaxLength: 16);
		var ids = tok.Encode("Hello world!");
		Console.WriteLine(string.Join(",", ids));
		var tokens = tok.Tokenize("Hello world!");
		Console.WriteLine(string.Join(" ", tokens));
	}
}


