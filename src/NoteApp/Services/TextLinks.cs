using System.Text.RegularExpressions;
using NoteApp.Domain.Functional;
using NoteApp.Domain.ValueObjects;

namespace NoteApp.Services;

// A web address found in free text: where it sits in the string, and where it leads.
public sealed record TextLink(int Start, int Length, LinkUrl Url);

// The addresses the editor makes clickable in text blocks, checklist items and link
// descriptions. Only what reads unambiguously as a web address counts — "http(s)://"
// or "www." up to the next whitespace — and the target must pass the same rule as a
// Link block (LinkUrl: absolute HTTP or HTTPS), so a click can never launch a file.
public static partial class TextLinks
{
    private const string WwwPrefix = "www.";

    // Sentence punctuation right after an address belongs to the sentence.
    private const string TrailingPunctuation = ".,;:!?'\"";

    [GeneratedRegex(@"\b(?:https?://|www\.)[^\s<>""]+", RegexOptions.IgnoreCase)]
    private static partial Regex Candidate();

    public static IReadOnlyList<TextLink> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var links = new List<TextLink>();
        foreach (Match match in Candidate().Matches(text))
        {
            var address = match.Value[..TrimmedLength(match.Value)];
            if (ToUrl(address) is Option<LinkUrl>.Some { Value: var url })
                links.Add(new TextLink(match.Index, address.Length, url));
        }

        return links;
    }

    // The address under a character, for Ctrl+Click: index is a character position in text.
    public static Option<LinkUrl> At(string? text, int index) =>
        Find(text).FirstOrDefault(l => index >= l.Start && index < l.Start + l.Length) is { } link
            ? new Option<LinkUrl>.Some(link.Url)
            : Option<LinkUrl>.Empty();

    public static Option<LinkUrl> First(string? text) =>
        Find(text) is [var link, ..] ? new Option<LinkUrl>.Some(link.Url) : Option<LinkUrl>.Empty();

    // The whole text is one address and nothing else: a checklist line that is a link.
    public static bool IsLink(string? text)
    {
        var trimmed = text?.Trim();
        return Find(trimmed) is [var link] && link.Start == 0 && link.Length == trimmed!.Length;
    }

    // A closing bracket ends the address only when it has no opening one inside it:
    // "(see https://en.wikipedia.org/wiki/Foo_(bar))" keeps "(bar)" and drops the last ")".
    private static int TrimmedLength(string candidate)
    {
        var length = candidate.Length;
        while (length > 0)
        {
            var last = candidate[length - 1];
            var unbalanced = last switch
            {
                ')' => IsUnbalanced(candidate, length, '(', ')'),
                ']' => IsUnbalanced(candidate, length, '[', ']'),
                '}' => IsUnbalanced(candidate, length, '{', '}'),
                _ => TrailingPunctuation.Contains(last)
            };

            if (!unbalanced)
                break;
            length--;
        }

        return length;
    }

    private static bool IsUnbalanced(string candidate, int length, char open, char close)
    {
        var span = candidate.AsSpan(0, length);
        return span.Count(close) > span.Count(open);
    }

    // "www.example.com" has no scheme: browsers assume HTTPS today, so do we.
    private static Option<LinkUrl> ToUrl(string address)
    {
        var isWww = address.StartsWith(WwwPrefix, StringComparison.OrdinalIgnoreCase);
        var prefixLength = isWww ? WwwPrefix.Length : address.IndexOf("://", StringComparison.Ordinal) + 3;
        if (address.Length <= prefixLength)
            return Option<LinkUrl>.Empty();

        return LinkUrl.From(isWww ? "https://" + address : address).TryGet(out var url, out _)
            ? new Option<LinkUrl>.Some(url)
            : Option<LinkUrl>.Empty();
    }
}
