using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using PDFtoImage;
using SkiaSharp;

namespace StudyAssistant.Services;

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
    // Returns the full text of the document, pages separated by double newline.
    public static async Task<string> LoadTextWithOcrAsync(string pdfPath, OCRService ocrService)
    {
        var pageImages = GetPageImages(pdfPath);
        var sb = new StringBuilder();

        for (int i = 0; i < pageImages.Count; i++)
        {
            Console.Write($"\r  OCR page {i + 1}/{pageImages.Count}...");
            var pageText = await ocrService.ReadPageAsync(pageImages[i]);
            sb.Append(pageText);
            sb.Append("\n\n");
        }

        Console.WriteLine();
        return sb.ToString();
    }

    // OCR a full PDF using Pix2Text — best for pages with math formulas.
    // Returns text with LaTeX math inline (e.g. "area is $\pi r^2$").
    public static async Task<string> LoadTextWithMathOcrAsync(string pdfPath, MathOcrService mathOcrService)
    {
        var pageImages = GetPageImages(pdfPath);
        var sb = new StringBuilder();

        for (int i = 0; i < pageImages.Count; i++)
        {
            Console.Write($"\r  Math OCR page {i + 1}/{pageImages.Count}...");
            var pageText = await mathOcrService.ReadPageAsync(pageImages[i]);
            sb.Append(pageText);
            sb.Append("\n\n");
        }

        Console.WriteLine();
        return sb.ToString();
    }

    // Original PdfPig text extraction — kept as fallback for simple text-only PDFs.
    public static string LoadText(string pdfPath)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(pdfPath);

        var sb = new StringBuilder();

        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        foreach (var page in document.GetPages())
        {
            sb.Append(page.Text);
            sb.Append("\n\n");
        }

        return sb.ToString();
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
    public static async Task<string> LoadTextWithSelectiveFormulaOcrAsync(string pdfPath, MathOcrService mathOcrService)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(pdfPath);

        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
        var pages = document.GetPages().ToList();
        List<byte[]>? pageImages = null; // rendered lazily — only if a page actually needs it

        var sb = new StringBuilder();
        int flaggedCount = 0;

        for (int i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            sb.Append(page.Text);

            if (PageHasEmbeddedFormulaFont(page))
            {
                flaggedCount++;
                pageImages ??= GetPageImages(pdfPath);
                if (i < pageImages.Count)
                {
                    Console.Write($"\r  Formula OCR page {i + 1}/{pages.Count}...");
                    var formulas = await mathOcrService.DetectFormulasAsync(pageImages[i]);
                    if (formulas.Count > 0)
                        sb.Append("\n\nFormulas on this page:\n" +
                                  string.Join("\n", formulas.Select(f => $"$${f}$$")));
                }
            }

            sb.Append("\n\n");
        }

        if (flaggedCount > 0) Console.WriteLine();
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
