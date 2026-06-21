using System.Globalization;

namespace IsaacAgent.Rag.Embedding;

/// <summary>
/// Minimal WordPiece tokenizer for BERT-style uncased models (e.g., all-MiniLM-L6-v2).
/// Not a full HuggingFace tokenizer replacement, but sufficient for sentence embeddings.
/// </summary>
internal sealed class WordPieceTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private const string UnknownToken = "[UNK]";
    private const string ClsToken = "[CLS]";
    private const string SepToken = "[SEP]";
    private const string PadToken = "[PAD]";
    private const int MaxInputChars = 10000;

    public WordPieceTokenizer(string vocabPath)
    {
        var lines = File.ReadAllLines(vocabPath);
        _vocab = new Dictionary<string, int>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
            _vocab[lines[i].Trim()] = i;
    }

    public (long[] InputIds, long[] AttentionMask) Encode(string text, int maxLength)
    {
        if (text.Length > MaxInputChars)
            text = text[..MaxInputChars];

        var tokens = new List<string> { ClsToken };
        var words = PreTokenize(text.ToLowerInvariant());
        foreach (var word in words)
        {
            var subTokens = WordPieceSplit(word);
            tokens.AddRange(subTokens);
            if (tokens.Count >= maxLength) break;
        }
        tokens.Add(SepToken);

        if (tokens.Count > maxLength)
            tokens = tokens[..maxLength];

        var inputIds = new long[tokens.Count];
        var attentionMask = new long[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            inputIds[i] = _vocab.TryGetValue(tokens[i], out var id) ? id : _vocab[UnknownToken];
            attentionMask[i] = 1;
        }

        return (inputIds, attentionMask);
    }

    private List<string> WordPieceSplit(string word)
    {
        var result = new List<string>();
        if (word.Length == 0) return result;

        if (_vocab.ContainsKey(word))
        {
            result.Add(word);
            return result;
        }

        var start = 0;
        while (start < word.Length)
        {
            var end = word.Length;
            var found = false;
            while (start < end)
            {
                var sub = word[start..end];
                var candidate = start == 0 ? sub : "##" + sub;
                if (_vocab.ContainsKey(candidate))
                {
                    result.Add(candidate);
                    start = end;
                    found = true;
                    break;
                }
                end--;
            }
            if (!found)
            {
                result.Add(UnknownToken);
                break;
            }
        }
        return result;
    }

    private static List<string> PreTokenize(string text)
    {
        var tokens = new List<string>();
        var current = new List<char>();

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (current.Count > 0)
                {
                    tokens.Add(new string(current.ToArray()));
                    current.Clear();
                }
            }
            else if (IsPunctuation(c))
            {
                if (current.Count > 0)
                {
                    tokens.Add(new string(current.ToArray()));
                    current.Clear();
                }
                tokens.Add(c.ToString());
            }
            else
            {
                current.Add(c);
            }
        }

        if (current.Count > 0)
            tokens.Add(new string(current.ToArray()));

        return tokens;
    }

    private static bool IsPunctuation(char c)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.ConnectorPunctuation
            or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation;
    }
}
