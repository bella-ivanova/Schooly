using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace StudyAssistant.Services;

public static class PDFLoader
{
    public static string LoadText(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(pdfPath);

        StringBuilder sb = new StringBuilder();

        using (PdfDocument document = PdfDocument.Open(pdfPath))
        {
            foreach (var page in document.GetPages())
            {
                sb.Append(page.Text);
                sb.Append("\n\n"); // preserve paragraph break between pages
            }
        }

        return sb.ToString();
    }

    public static string CleanText(string text)
    {
        // Normalize line endings
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Fix hyphenated word breaks (e.g. "photosyn-\nthesis" → "photosynthesis")
        text = Regex.Replace(text, @"(\w)-\n(\w)", "$1$2");

        // Clean spaces within each line and drop pure-digit lines (page numbers)
        var lines = text.Split('\n')
            .Select(line => Regex.Replace(line, @"[ \t]+", " ").Trim())
            .Where(line => !Regex.IsMatch(line, @"^\d+$"));

        text = string.Join("\n", lines);

        // Collapse 3+ blank lines into a paragraph break
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Join soft line breaks within paragraphs (word-wrap artifacts → single space)
        var paragraphs = text
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Regex.Replace(p.Trim(), @"\n", " "))
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join("\n\n", paragraphs).Trim();
    }

    // Splits text on paragraph and sentence boundaries with sentence overlap between chunks.
    public static List<string> ChunkText(string text, int maxChunkSize = 400, int overlapSentences = 1)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
                             .Where(p => !string.IsNullOrWhiteSpace(p));

        var currentChunk = new StringBuilder();
        var overlapBuffer = new List<string>(); // last N sentences of the previous chunk

        foreach (var paragraph in paragraphs)
        {
            // Paragraph fits in the current chunk — append it
            if (currentChunk.Length + paragraph.Length + 2 <= maxChunkSize)
            {
                if (currentChunk.Length > 0) currentChunk.Append("\n\n");
                currentChunk.Append(paragraph);
            }
            else
            {
                // Flush the current chunk
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    overlapBuffer = GetLastSentences(currentChunk.ToString(), overlapSentences);
                    currentChunk.Clear();

                    // Start next chunk with overlap, capped at half of maxChunkSize
                    if (overlapBuffer.Count > 0)
                    {
                        var overlapText = string.Join(" ", overlapBuffer);
                        if (overlapText.Length > maxChunkSize / 2)
                            overlapText = overlapText[..(maxChunkSize / 2)];
                        currentChunk.Append(overlapText);
                    }
                }

                // Paragraph is larger than the max — split into sentences,
                // then split any sentence still too long by word boundary.
                if (paragraph.Length > maxChunkSize)
                {
                    var pieces = SplitSentences(paragraph)
                        .SelectMany(s => s.Length <= maxChunkSize
                            ? new[] { s }
                            : SplitByLength(s, maxChunkSize))
                        .ToList();

                    foreach (var piece in pieces)
                    {
                        if (currentChunk.Length + piece.Length + 1 <= maxChunkSize)
                        {
                            if (currentChunk.Length > 0) currentChunk.Append(' ');
                            currentChunk.Append(piece);
                        }
                        else
                        {
                            if (currentChunk.Length > 0)
                            {
                                chunks.Add(currentChunk.ToString());
                                overlapBuffer = GetLastSentences(currentChunk.ToString(), overlapSentences);
                                currentChunk.Clear();

                                if (overlapBuffer.Count > 0)
                                {
                                    var overlapText = string.Join(" ", overlapBuffer);
                                    if (overlapText.Length > maxChunkSize / 2)
                                        overlapText = overlapText[..(maxChunkSize / 2)];
                                    currentChunk.Append(overlapText);
                                }
                            }

                            if (currentChunk.Length > 0) currentChunk.Append(' ');
                            currentChunk.Append(piece);
                        }
                    }
                }
                else
                {
                    if (currentChunk.Length > 0) currentChunk.Append("\n\n");
                    currentChunk.Append(paragraph);
                }
            }
        }

        if (currentChunk.Length > 0)
            chunks.Add(currentChunk.ToString());

        return chunks;
    }

    // Splits text at word boundaries so no part exceeds maxLength.
    private static IEnumerable<string> SplitByLength(string text, int maxLength)
    {
        var words = text.Split(' ');
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length + word.Length + (current.Length > 0 ? 1 : 0) <= maxLength)
            {
                if (current.Length > 0) current.Append(' ');
                current.Append(word);
            }
            else
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                // Single word longer than maxLength — yield it as-is
                if (word.Length > maxLength)
                    yield return word;
                else
                    current.Append(word);
            }
        }

        if (current.Length > 0)
            yield return current.ToString();
    }

    // Splits a block of text into individual sentences.
    private static List<string> SplitSentences(string text)
    {
        return Regex.Split(text, @"(?<=[.!?])\s+(?=\p{Lu})")
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
    }

    // Returns the last N sentences from a chunk for use as overlap.
    private static List<string> GetLastSentences(string chunk, int count)
    {
        var sentences = SplitSentences(chunk);
        return sentences.Count <= count ? sentences : sentences.Skip(sentences.Count - count).ToList();
    }
}
