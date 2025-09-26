namespace FlashTokenizer;

/// <summary>
/// Public facade for WordPiece/BERT tokenization, ready for app consumption.
/// </summary>
public sealed class FlashTokenizer : ITokenizer
{
	private readonly FlashBertTokenizer _impl;
    private readonly FlashBertTokenizerBidirectional? _implBi;
    private readonly BpeTokenizer? _bpe;

	/// <summary>
	/// Create a tokenizer using provided options.
	/// </summary>
	/// <param name="vocabPath">Path to vocab.txt. If null, defaults to sample/vocab.txt.</param>
	/// <param name="doLowerCase">Lowercase and strip accents.</param>
	/// <param name="modelMaxLength">Max length; -1 for unlimited.</param>
	/// <param name="enableBidirectional">Enable bidirectional fallback heuristic.</param>
	public FlashTokenizer(string? vocabPath = null, bool doLowerCase = true, int modelMaxLength = 128, bool enableBidirectional = false)
	{
		vocabPath ??= Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample", "vocab.txt");
		vocabPath = Path.GetFullPath(vocabPath);
		if (enableBidirectional)
		{
			_impl = new FlashBertTokenizer(vocabPath, doLowerCase, modelMaxLength);
			_implBi = new FlashBertTokenizerBidirectional(vocabPath, doLowerCase, modelMaxLength);
		}
		else
		{
			_impl = new FlashBertTokenizer(vocabPath, doLowerCase, modelMaxLength);
		}
	}

	/// <summary>
	/// Create a tokenizer using a TokenizerOptions object.
	/// </summary>
	public FlashTokenizer(TokenizerOptions options)
	{
		if (options.Type == TokenizerType.BPE)
		{
			_bpe = new BpeTokenizer(options.BpeVocabJsonPath ?? throw new ArgumentNullException(nameof(options.BpeVocabJsonPath)), options.BpeMergesPath ?? throw new ArgumentNullException(nameof(options.BpeMergesPath)));
			_impl = new FlashBertTokenizer(options.VocabPath ?? string.Empty, options.DoLowerCase, options.ModelMaxLength); // unused
			_implBi = null;
			return;
		}
		var vocabPath = options.VocabPath ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "sample", "vocab.txt");
		vocabPath = Path.GetFullPath(vocabPath);
		if (options.EnableBidirectional)
		{
			_impl = new FlashBertTokenizer(vocabPath, options.DoLowerCase, options.ModelMaxLength);
			_implBi = new FlashBertTokenizerBidirectional(vocabPath, options.DoLowerCase, options.ModelMaxLength);
		}
		else
		{
			_impl = new FlashBertTokenizer(vocabPath, options.DoLowerCase, options.ModelMaxLength);
			_implBi = null;
		}
	}

	/// <inheritdoc />
	public List<int> Encode(string text, string padding = "max_length", int maxLength = -1)
		=> _bpe is not null ? _bpe.Encode(text) : (_implBi is null ? _impl.Encode(text, padding, maxLength) : _implBi.Encode(text, padding, maxLength));

	/// <inheritdoc />
	public string Decode(IEnumerable<int> ids)
		=> _bpe is not null ? _bpe.Decode(ids) : _impl.Decode(ids);

	// ITokenizer compatibility overload
	public List<int> Encode(string input) => Encode(input, "max_length", -1);
}


