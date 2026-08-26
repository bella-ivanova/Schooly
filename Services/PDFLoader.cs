using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using PDFtoImage;
using SkiaSharp;

namespace StudyAssistant.Services;

// One page's extracted text, tagged with its 1-indexed page number so chunking
// (see ChunkPages) can enforce page boundaries as hard chunk-split points.
public readonly record struct PageText(int PageNumber, string Text);

public static class PDFLoader
{
    // Converts every page of a PDF into PNG image bytes.
    // Each item in the returned list is one page as a PNG byte array.
    public static List<byte[]> GetPageImages(string pdfPath, int dpi = 96)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(pdfPath);

        var pages = new List<byte[]>();
        var pdfBytes = File.ReadAllBytes(pdfPath);

        foreach (var skBitmap in Conversion.ToImages(pdfBytes, options: new RenderOptions(Dpi: dpi)))
        {
            using (skBitmap)
            {
                using var image = SKImage.FromBitmap(skBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                pages.Add(data.ToArray());
            }
        }

        return pages;
    }

    // OCR a full PDF using a vision model — one page at a time.
    // Returns one PageText per page, 1-indexed.
    public static async Task<List<PageText>> LoadTextWithOcrAsync(string pdfPath, OCRService ocrService)
    {
        var pageImages = GetPageImages(pdfPath);
        var pages = new List<PageText>();

        for (int i = 0; i < pageImages.Count; i++)
        {
            Console.Write($"\r  OCR page {i + 1}/{pageImages.Count}...");
            var pageText = await ocrService.ReadPageAsync(pageImages[i]);
            pages.Add(new PageText(i + 1, pageText));
        }

        Console.WriteLine();
        return pages;
    }

    // OCR a full PDF using Pix2Text — best for pages with math formulas.
    // Returns one PageText per page, 1-indexed, with LaTeX math inline (e.g. "area is $\pi r^2$").
    public static async Task<List<PageText>> LoadTextWithMathOcrAsync(string pdfPath, MathOcrService mathOcrService)
    {
        var pageImages = GetPageImages(pdfPath);
        var pages = new List<PageText>();

        for (int i = 0; i < pageImages.Count; i++)
        {
            Console.Write($"\r  Math OCR page {i + 1}/{pageImages.Count}...");
            var pageText = await mathOcrService.ReadPageAsync(pageImages[i]);
            pages.Add(new PageText(i + 1, pageText));
        }

        Console.WriteLine();
        return pages;
    }

    // Original PdfPig text extraction — kept as fallback for simple text-only PDFs.
    // Returns one PageText per page, 1-indexed.
    public static List<PageText> LoadText(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(pdfPath);

        var pages = new List<PageText>();

        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        int i = 0;
        foreach (var page in document.GetPages())
        {
            i++;
            pages.Add(new PageText(i, page.Text));
        }

        return pages;
    }

    // Classic MathType/Equation-Editor export fonts. A page containing any of these
    // almost certainly has an embedded equation object that PdfPig's plain text
    // extraction will have flattened into fragmented (but symbolically present) text.
    private static readonly string[] FormulaFontMarkers = { "Symbol", "MTExtra", "MT-Extra" };

    private static bool PageHasEmbeddedFormulaFont(UglyToad.PdfPig.Content.Page page) =>
        page.Letters.Any(l => FormulaFontMarkers.Any(marker =>
            l.FontName.Contains(marker, StringComparison.OrdinalIgnoreCase)));

    // PdfPig text extraction is the default for every page — already correct for
    // machine-readable PDFs (no OCR, no language dependency). Pages that embed
    // classic MathType/Equation-Editor fonts additionally get a formula-only
    // Pix2Text pass (LatexOCR, never the general-text CnOcr path), appended after
    // that page's prose rather than spliced in place.
    public static async Task<List<PageText>> LoadTextWithSelectiveFormulaOcrAsync(string pdfPath, MathOcrService mathOcrService)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(pdfPath);

        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        var pdfPages = document.GetPages().ToList();
        List<byte[]>? pageImages = null; // rendered lazily — only if a page actually needs it

        var result = new List<PageText>();
        int flaggedCount = 0;

        for (int i = 0; i < pdfPages.Count; i++)
        {
            var page = pdfPages[i];
            var sb = new StringBuilder(page.Text);

            if (PageHasEmbeddedFormulaFont(page))
            {
                flaggedCount++;
                pageImages ??= GetPageImages(pdfPath);
                if (i < pageImages.Count)
                {
                    Console.Write($"\r  Formula OCR page {i + 1}/{pdfPages.Count}...");
                    var formulas = await mathOcrService.DetectFormulasAsync(pageImages[i]);
                    if (formulas.Count > 0)
                        sb.Append("\n\nFormulas on this page:\n" +
                                  string.Join("\n", formulas.Select(f => $"$${f}$$")));
                }
            }

            result.Add(new PageText(i + 1, sb.ToString()));
        }

        if (flaggedCount > 0) Console.WriteLine();
        return result;
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

    // Below this many cleaned characters, a page is treated as noise (a blank divider
    // page or a page whose only extractable text was a bare page number, which CleanText
    // already reduces to 0 chars) rather than content worth embedding. Verified against
    // the Grade 10 Math corpus: the shortest real page is 59 chars (a title page), so 20
    // sits with margin below every real page and above the 0-char noise case.
    private const int MinPageChunkChars = 20;

    // Chunks each page independently so a chunk never spans two pages — CleanText and
    // ChunkText run once per page, which also means ChunkText's overlap buffer (scoped
    // to a single call) can never carry text across a page boundary. Pages below
    // MinPageChunkChars after cleaning are skipped (logged, not silently dropped) rather
    // than merged into a neighboring page, since merging would reintroduce the exact
    // cross-page bug this exists to fix.
    public static List<(string Text, int PageNumber)> ChunkPages(
        List<PageText> pages, int maxChunkSize = 400, int overlapSentences = 1)
    {
        var result = new List<(string Text, int PageNumber)>();

        foreach (var page in pages)
        {
            var cleaned = CleanText(page.Text);
            if (cleaned.Length < MinPageChunkChars)
            {
                Console.WriteLine($"  Skipping page {page.PageNumber} — only {cleaned.Length} chars after cleaning.");
                continue;
            }

            foreach (var chunk in ChunkText(cleaned, maxChunkSize, overlapSentences))
                result.Add((chunk, page.PageNumber));
        }

        return result;
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
