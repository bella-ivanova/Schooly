namespace StudyAssistant.Services;

// IMPORTANT: Register as Scoped, never Singleton.
// _currentGrade and _temporaryChunks are per-user state; a Singleton would share them across all users.
// When wiring HTTP endpoints, always set _currentGrade from the authenticated user's JWT claims.
public class RAGService
{
    private readonly EmbeddingService _embeddingService;
    private readonly IChatService _chat;
    private readonly QdrantService _qdrant;
    private readonly OCRService? _ocr;
    private readonly MathOcrService? _mathOcr;

    // In-memory store for temporary PDFs loaded during the current chat session only.
    // These are never saved to Qdrant — they disappear when the session ends.
    private readonly List<(string Text, float[] Embedding, string Subject)> _temporaryChunks = new();

    private int _currentGrade = 0;

    public RAGService(IChatService chat, EmbeddingService embeddingService, QdrantService qdrant, OCRService? ocr = null, MathOcrService? mathOcr = null)
    {
        _chat             = chat;
        _embeddingService = embeddingService;
        _qdrant           = qdrant;
        _ocr              = ocr;
        _mathOcr          = mathOcr;
    }

    // Sets the student's current grade. Qdrant already holds all grades —
    // we just remember the number so searches are filtered to grades 1 → N.
    public void SetGrade(int grade)
    {
        _currentGrade = grade;
        Console.WriteLine($"Grade set to {grade}.");
    }

    // Seeds prior conversation turns into the underlying chat service before the next
    // AskStreamAsync call, so a resumed session has real multi-turn context. Must be
    // called before AskStreamAsync in the same request — IChatService's history is
    // Scoped and empty at the start of every HTTP request.
    public void SeedHistory(IEnumerable<(string Role, string Content)> turns) => _chat.SeedHistory(turns);

    // Reads PDFs from Database/DataPdf/Grade{grade}/{Subject}/ subfolders.
    // Each subfolder name becomes the subject tag for its chunks.
    // Falls back to scanning the grade root for PDFs with no subject tag.
    public async Task IngestGradePDFsAsync(int grade)
    {
        var gradeFolder = Path.Combine("Database", "DataPdf", $"Grade{grade}");

        if (!Directory.Exists(gradeFolder))
        {
            Console.WriteLine($"No PDF folder found at {gradeFolder}");
            return;
        }

        // Ask Qdrant which files are already stored for this grade
        await _qdrant.EnsureCollectionAsync();
        var alreadyIngested = (await _qdrant.GetIngestedFilesAsync(grade)).ToHashSet();

        int totalIngested = 0;
        int skipped = 0;

        async Task IngestFile(string pdfPath, string subject, string fileKey)
        {
            if (alreadyIngested.Contains(fileKey))
            {
                Console.WriteLine($"Skipping (already ingested): {fileKey}");
                skipped++;
                return;
            }

            Console.WriteLine($"Ingesting {fileKey}...");

            int chunkCount;
            try
            {
                chunkCount = await IngestSingleFileAsync(pdfPath, grade, subject, fileKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nEmbedding failed: {ex.Message}");
                Console.WriteLine($"Is '{_embeddingService.Model}' pulled? Run: ollama pull {_embeddingService.Model}");
                Console.WriteLine($"Ingestion stopped. {totalIngested} file(s) were processed before the error.");
                return;
            }

            totalIngested++;
            Console.WriteLine($"  → {chunkCount} chunks stored in Qdrant");
        }

        // Scan subject subfolders (e.g. Grade5/Math/, Grade5/Science/)
        foreach (var subjectFolder in Directory.GetDirectories(gradeFolder))
        {
            var subject = Path.GetFileName(subjectFolder);
            foreach (var pdfPath in Directory.GetFiles(subjectFolder, "*.pdf"))
            {
                var fileKey = Path.Combine(subject, Path.GetFileName(pdfPath));
                await IngestFile(pdfPath, subject, fileKey);
            }
        }

        // Also scan PDFs directly in the grade root (no subject tag)
        foreach (var pdfPath in Directory.GetFiles(gradeFolder, "*.pdf"))
        {
            var fileKey = Path.GetFileName(pdfPath);
            await IngestFile(pdfPath, subject: "", fileKey);
        }

        if (totalIngested == 0 && skipped == 0)
        {
            Console.WriteLine($"No PDF files found in {gradeFolder}");
            return;
        }

        var skipMsg = skipped > 0 ? $" ({skipped} skipped)" : "";
        Console.WriteLine($"Grade {grade} ingestion complete. {totalIngested} file(s) ingested{skipMsg}.");
    }

    // Runs the OCR → chunk → embed → upsert pipeline for a single PDF already on disk.
    private async Task<int> IngestSingleFileAsync(string pdfPath, int grade, string subject, string fileKey)
    {
        // Priority: Pix2Text (best for math) → vision OCR → plain PdfPig text
        var text = _mathOcr != null
            ? await PDFLoader.LoadTextWithMathOcrAsync(pdfPath, _mathOcr)
            : _ocr != null
                ? await PDFLoader.LoadTextWithOcrAsync(pdfPath, _ocr)
                : PDFLoader.LoadText(pdfPath);
        text = PDFLoader.CleanText(text);
        var chunks = PDFLoader.ChunkText(text);

        var embeddings = await _embeddingService.GetEmbeddingsAsync(chunks);

        var chunkDataList = new List<ChunkData>();
        for (int i = 0; i < chunks.Count; i++)
        {
            chunkDataList.Add(new ChunkData
            {
                Text       = chunks[i],
                Embedding  = embeddings[i],
                Subject    = subject,
                Grade      = grade,
                SourceFile = fileKey
            });
        }

        await _qdrant.UpsertChunksAsync(chunkDataList);
        return chunks.Count;
    }

    // Saves an admin-uploaded PDF under Database/DataPdf/Grade{grade}/{subject}/ and ingests it into Qdrant.
    // overwrite=false rejects a duplicate fileKey; overwrite=true deletes the existing chunks first.
    public async Task<(bool Success, string? Error, int ChunkCount)> IngestUploadedFileAsync(
        int grade, string subject, string fileName, Stream content, bool overwrite)
    {
        var fileKey = string.IsNullOrWhiteSpace(subject) ? fileName : Path.Combine(subject, fileName);

        await _qdrant.EnsureCollectionAsync();
        var alreadyIngested = (await _qdrant.GetIngestedFilesAsync(grade)).ToHashSet();
        if (alreadyIngested.Contains(fileKey))
        {
            if (!overwrite)
                return (false, $"File '{fileKey}' already exists for grade {grade}.", 0);
            await DeleteGradeFileAsync(grade, fileKey);
        }

        var folder = Path.Combine("Database", "DataPdf", $"Grade{grade}", subject);
        Directory.CreateDirectory(folder);
        var pdfPath = Path.Combine(folder, fileName);

        await using (var fs = File.Create(pdfPath))
            await content.CopyToAsync(fs);

        var chunkCount = await IngestSingleFileAsync(pdfPath, grade, subject, fileKey);
        return (true, null, chunkCount);
    }

    // Adds a PDF temporarily for the current chat session only (not saved to Qdrant).
    public async Task<int> AddTemporaryPDFAsync(string pdfPath, string subject = "")
    {
        var text = PDFLoader.LoadText(pdfPath);
        text = PDFLoader.CleanText(text);
        var chunks = PDFLoader.ChunkText(text);
        var embeddings = await _embeddingService.GetEmbeddingsAsync(chunks);

        for (int i = 0; i < chunks.Count; i++)
            _temporaryChunks.Add((chunks[i], embeddings[i], subject));

        var label = string.IsNullOrWhiteSpace(subject) ? "" : $" [{subject}]";
        Console.WriteLine($"Loaded '{Path.GetFileName(pdfPath)}'{label} temporarily — {chunks.Count} chunks.");
        return chunks.Count;
    }

    public void ClearTemporaryChunks() => _temporaryChunks.Clear();

    // Returns the list of ingested file keys for a given grade from Qdrant.
    public async Task<List<string>> GetIngestedFilesAsync(int grade) =>
        await _qdrant.GetIngestedFilesAsync(grade);

    // Deletes a specific file's chunks from Qdrant.
    public async Task<bool> DeleteGradeFileAsync(int grade, string fileKey)
    {
        var files = await _qdrant.GetIngestedFilesAsync(grade);
        if (!files.Contains(fileKey))
        {
            Console.WriteLine($"File '{fileKey}' not found in Grade {grade}.");
            return false;
        }

        await _qdrant.DeleteFileAsync(grade, fileKey);
        Console.WriteLine($"Deleted '{fileKey}' from Grade {grade}.");
        return true;
    }

    // Returns the raw formatted context string for a query (embedding + Qdrant + temp chunks).
    // Returns empty string if nothing is found or Qdrant is unreachable.
    private async Task<string> GetContextAsync(string query)
    {
        if (_currentGrade == 0 && _temporaryChunks.Count == 0)
            return "";

        var qEmbeddingList = await _embeddingService.GetEmbeddingsAsync(new List<string> { query });
        if (qEmbeddingList.Count == 0) return "";
        var queryEmbedding = qEmbeddingList[0];

        var combinedChunks = new List<(string Text, string Subject, int Grade)>();

        if (_currentGrade > 0)
        {
            try
            {
                var qdrantResults = await _qdrant.SearchAsync(
                    queryEmbedding,
                    topK:        10,
                    minScore:    0.1f,
                    gradeFilter: _currentGrade
                );
                foreach (var r in qdrantResults)
                    combinedChunks.Add((r.Text, r.Subject, r.Grade));
            }
            catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("Unavailable"))
            {
                Console.WriteLine("\n[Qdrant is not running — answering without textbook context.]");
                Console.WriteLine("Start it with: docker-compose up qdrant\n");
                return "";
            }
        }

        if (_temporaryChunks.Count > 0)
        {
            var tempResults = _temporaryChunks
                .Select(x => new { x.Text, x.Subject, Score = CosineSimilarity(queryEmbedding, x.Embedding) })
                .Where(x => x.Score >= 0.1f)
                .OrderByDescending(x => x.Score)
                .Take(5)
                .Select(x => (x.Text, x.Subject, Grade: _currentGrade));
            combinedChunks.AddRange(tempResults);
        }

        if (combinedChunks.Count == 0)
            return "";

        var formattedChunks = combinedChunks.Select(c =>
        {
            var label = $"[Grade {c.Grade}";
            if (!string.IsNullOrWhiteSpace(c.Subject)) label += $" / {c.Subject}";
            label += "]";
            return $"{label}\n{c.Text}";
        });

        return string.Join("\n\n", formattedChunks);
    }

    // Returns the raw context chunks for a topic query without calling the LLM.
    // Used by ExamService to retrieve relevant textbook material.
    public async Task<string> GetChunksAsync(string query) => await GetContextAsync(query);

    // capture = true  → suppresses <GEOM> block from console and returns the full
    //                    LLM response so the caller can extract and visualise it.
    // capture = false → original behaviour, returns null.
    //
    // instructionSuffix → appended to the API message (not stored in history) so
    //                     the LLM receives extra instructions (e.g. the GEOM prompt)
    //                     without polluting the conversation history shown to students.
    public async Task<string?> Ask(string question, bool capture = false, string? instructionSuffix = null)
    {
        question = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);

        Task<string?> Send(string userMsg, string? apiMsg, string? sysOverride = null)
        {
            var finalApi = instructionSuffix != null
                ? (apiMsg ?? userMsg) + "\n\n" + instructionSuffix
                : apiMsg;

            if (capture)
                return _chat.StreamMessageFilteredAsync(userMsg, finalApi, sysOverride)
                            .ContinueWith(t => (string?)t.Result);
            else
                return _chat.StreamMessageAsync(userMsg, finalApi, sysOverride)
                            .ContinueWith(_ => (string?)null);
        }

        var context = await GetContextAsync(question);

        if (string.IsNullOrEmpty(context))
            return await Send(question, null);

        var gradeLabel = _currentGrade > 0 ? $"Grade {_currentGrade}" : "the student's current grade";

        // System-role: all tutor persona and behavior rules (student cannot override these).
        var systemOverride =
            $"You are a school tutor. The student is in {gradeLabel}. The textbook excerpts below are from their curriculum (content may span multiple grade levels and may be in a different language — translate as needed).\n" +
            "Each excerpt is labeled with its grade and subject. A unit title may differ from what the student calls the topic — e.g. 'Solving Triangles' covers trigonometry.\n" +
            "IMPORTANT: Base your answer strictly on what is present in the excerpts. Do NOT say a topic is absent unless no related content appears in the excerpts.\n" +
            "Identify the relevant excerpt, then apply its definitions, formulas, and methods step by step.\n" +
            "The student's problem may be a new example — use the textbook method with the student's numbers.\n" +
            "Students know all prerequisites for concepts present in their grade material.\n" +
            "Keep simple answers short; provide detailed explanations for complex topics.\n" +
            "Only say 'This is not covered in your current grade.' if the subject is entirely absent from the material.\n" +
            "Respond in the same language the student used in their question.";

        // User-role: textbook context as reference data, then the student's question clearly delimited.
        var ragUserContent =
            "--- Textbook Material ---\n" +
            context +
            "\n--- End of Material ---\n\n" +
            "<student_question>\n" + question + "\n</student_question>";

        return await Send(question, ragUserContent, systemOverride);
    }

    // HTTP streaming variant of Ask(). Yields tokens as they arrive so ChatController
    // can write them to an SSE response. Chat-log saving is the caller's responsibility.
    public async IAsyncEnumerable<string> AskStreamAsync(string question)
    {
        question = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);

        var context = await GetContextAsync(question);

        string? apiMsg      = null;
        string? sysOverride = null;

        if (!string.IsNullOrEmpty(context))
        {
            var gradeLabel = _currentGrade > 0 ? $"Grade {_currentGrade}" : "the student's current grade";

            sysOverride =
                $"You are a school tutor. The student is in {gradeLabel}. The textbook excerpts below are from their curriculum (content may span multiple grade levels and may be in a different language — translate as needed).\n" +
                "Each excerpt is labeled with its grade and subject. A unit title may differ from what the student calls the topic — e.g. 'Solving Triangles' covers trigonometry.\n" +
                "IMPORTANT: Base your answer strictly on what is present in the excerpts. Do NOT say a topic is absent unless no related content appears in the excerpts.\n" +
                "Identify the relevant excerpt, then apply its definitions, formulas, and methods step by step.\n" +
                "The student's problem may be a new example — use the textbook method with the student's numbers.\n" +
                "Students know all prerequisites for concepts present in their grade material.\n" +
                "Keep simple answers short; provide detailed explanations for complex topics.\n" +
                "Only say 'This is not covered in your current grade.' if the subject is entirely absent from the material.\n" +
                "Respond in the same language the student used in their question.";

            apiMsg =
                "--- Textbook Material ---\n" +
                context +
                "\n--- End of Material ---\n\n" +
                "<student_question>\n" + question + "\n</student_question>";
        }

        await foreach (var token in _chat.StreamTokensAsync(question, apiMsg, sysOverride))
            yield return token;
    }

    // Used locally for temporary chunk similarity (Qdrant handles this for permanent chunks)
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0) return 0;
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }
}
