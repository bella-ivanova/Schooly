using System.Text.Json;

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
    private readonly LanguageDetectionService _languageDetector;
    private readonly StemSubjectClassifier _stemClassifier;
    private readonly StemAnswerPipelineService _stemPipeline;
    private readonly StereoModelGenerationService _stereoGen;
    private readonly StemPipelineOptions _stemOptions;
    private readonly ILogger<RAGService> _logger;

    // In-memory store for temporary PDFs loaded during the current chat session only.
    // These are never saved to Qdrant — they disappear when the session ends.
    private readonly List<(string Text, float[] Embedding, string Subject)> _temporaryChunks = new();

    private int _currentGrade = 0;

    public RAGService(IChatService chat, EmbeddingService embeddingService, QdrantService qdrant,
        LanguageDetectionService languageDetector, StemSubjectClassifier stemClassifier,
        StemAnswerPipelineService stemPipeline, StereoModelGenerationService stereoGen,
        StemPipelineOptions stemOptions, ILogger<RAGService> logger,
        OCRService? ocr = null, MathOcrService? mathOcr = null)
    {
        _chat             = chat;
        _embeddingService = embeddingService;
        _qdrant           = qdrant;
        _languageDetector = languageDetector;
        _stemClassifier   = stemClassifier;
        _stemPipeline     = stemPipeline;
        _stereoGen        = stereoGen;
        _stemOptions      = stemOptions;
        _logger           = logger;
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
        // PdfPig's plain text is the default for every page (correct for machine-readable
        // PDFs, no OCR/language dependency). Pages with embedded formula fonts additionally
        // get a formula-only Pix2Text pass (LatexOCR, never the general-text CnOcr path)
        // appended after the page's prose. Vision OCR is the fallback only when Pix2Text
        // itself is unavailable (e.g. a genuinely scanned, image-only PDF).
        var pages = _mathOcr != null
            ? await PDFLoader.LoadTextWithSelectiveFormulaOcrAsync(pdfPath, _mathOcr)
            : _ocr != null
                ? await PDFLoader.LoadTextWithOcrAsync(pdfPath, _ocr)
                : PDFLoader.LoadText(pdfPath);
        var chunks = PDFLoader.ChunkPages(pages);

        var embeddings = await _embeddingService.GetDocumentEmbeddingsAsync(chunks.Select(c => c.Text).ToList());

        var chunkDataList = new List<ChunkData>();
        for (int i = 0; i < chunks.Count; i++)
        {
            chunkDataList.Add(new ChunkData
            {
                Text       = chunks[i].Text,
                Embedding  = embeddings[i],
                Subject    = subject,
                Grade      = grade,
                SourceFile = fileKey,
                PageNumber = chunks[i].PageNumber
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
        var pages = PDFLoader.LoadText(pdfPath);
        var chunks = PDFLoader.ChunkPages(pages);
        var embeddings = await _embeddingService.GetDocumentEmbeddingsAsync(chunks.Select(c => c.Text).ToList());

        for (int i = 0; i < chunks.Count; i++)
            _temporaryChunks.Add((chunks[i].Text, embeddings[i], subject));

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

        var qEmbeddingList = await _embeddingService.GetQueryEmbeddingsAsync(new List<string> { query });
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
        var languageName = _languageDetector.DetectLanguageName(question);
        var languageInstruction = languageName is not null
            ? $"Respond entirely in {languageName}."
            : "Respond in the same language the student used in their question.";

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
            languageInstruction;

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
    //
    // Runs RAG context retrieval and STEM subject classification concurrently (two
    // independent operations — classification only calls _chat.OneShotAsync, which never
    // touches the shared conversation history) rather than stacking their latencies, then
    // routes math/physics/chemistry questions through the structured Qwen->BgGPT pipeline
    // and everything else through the original single-stage path, unchanged.
    public async IAsyncEnumerable<string> AskStreamAsync(string question)
    {
        question = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);

        var contextTask = GetContextAsync(question);
        var subjectTask = _stemClassifier.ClassifyAsync(question);
        var context = await contextTask;
        var subject = await subjectTask;

        var languageName = _languageDetector.DetectLanguageName(question);

        if (string.IsNullOrEmpty(context))
        {
            yield return languageName == "English"
                ? "No material was found for this question in the available textbooks. Try a different question or topic."
                : "Няма намерен материал по този въпрос в наличните учебници. Опитай с друг въпрос или тема.";
            yield break;
        }

        if (subject != StemSubject.None)
        {
            var stemStream = _stemOptions.DirectAnswer
                ? AskStemDirectStreamAsync(question, context, languageName)
                : AskStemStreamAsync(question, context, languageName);
            await foreach (var token in stemStream)
                yield return token;
            yield break;
        }

        await foreach (var token in AskGenericStreamAsync(question, context, languageName))
            yield return token;
    }

    // Original single-stage answer path (BgGPT only, given the RAG context directly),
    // extracted verbatim from what used to be AskStreamAsync's body so it can serve both
    // non-STEM questions and the STEM pipeline's total-failure safety net below.
    private async IAsyncEnumerable<string> AskGenericStreamAsync(string question, string context, string? languageName)
    {
        var gradeLabel = _currentGrade > 0 ? $"Grade {_currentGrade}" : "the student's current grade";
        var languageInstruction = languageName is not null
            ? $"Respond entirely in {languageName}."
            : "Respond in the same language the student used in their question.";

        var sysOverride =
            $"You are a school tutor. The student is in {gradeLabel}. The textbook excerpts below are from their curriculum (content may span multiple grade levels and may be in a different language — translate as needed).\n" +
            "Each excerpt is labeled with its grade and subject. A unit title may differ from what the student calls the topic — e.g. 'Solving Triangles' covers trigonometry.\n" +
            "IMPORTANT: Base your answer strictly on what is present in the excerpts. Do NOT say a topic is absent unless no related content appears in the excerpts.\n" +
            "Identify the relevant excerpt, then apply its definitions, formulas, and methods step by step.\n" +
            "The student's problem may be a new example — use the textbook method with the student's numbers.\n" +
            "Students know all prerequisites for concepts present in their grade material.\n" +
            "Keep simple answers short; provide detailed explanations for complex topics.\n" +
            "Only say 'This is not covered in your current grade.' if the subject is entirely absent from the material.\n" +
            StemAnswerPipelineService.GeometryDisambiguationNote + "\n" +
            languageInstruction;

        if (StereometryDetector.IsStereometryQuestion(question))
            sysOverride += "\n" + StereometryService.Instruction;

        var apiMsg =
            "--- Textbook Material ---\n" +
            context +
            "\n--- End of Material ---\n\n" +
            "<student_question>\n" + question + "\n</student_question>";

        await foreach (var token in _chat.StreamTokensAsync(question, apiMsg, sysOverride))
            yield return token;
    }

    // STEM structured-handoff path: Qwen reasons into structured JSON (or, on failure,
    // plain prose), then BgGPT narrates the result in the student's language — still
    // streamed via the same _chat.StreamTokensAsync mechanism AskGenericStreamAsync uses,
    // so ChatController's SSE framing never has to know this branch exists. If the
    // reasoning stage fails entirely (structured JSON exhausted AND the prose fallback also
    // fails), degrades to the generic path rather than surfacing an error — a student should
    // never see a broken STEM answer.
    private async IAsyncEnumerable<string> AskStemStreamAsync(string question, string context, string? languageName)
    {
        var languageInstruction = languageName is not null
            ? $"Respond entirely in {languageName}."
            : "Respond in the same language the student used in their question.";

        // Kick off Qwen's reasoning call first -- it gates the first content the student
        // sees, so its request should reach Ollama's queue ahead of anything else. Scene
        // generation (BgGPT, _chat) is started right after, still hoping to overlap with
        // Qwen's think:true call (~112s observed) where the server allows it, but its
        // result is NOT awaited here -- the scene block is invisible to the student until
        // spliced onto the very end of the stream (and filtered out of visible tokens
        // entirely), so it must never delay narration from starting. Safe to share _chat
        // between scene generation and narration below: GenerateSceneAsync only ever uses
        // a fresh per-call temp message list and never touches _chat's shared _messages
        // history, so it can't race with StreamTokensAsync's history append.
        var resultTask = _stemPipeline.SolveAsync(question, context);

        var isStereometry = StereometryDetector.IsStereometryQuestion(question);
        var sceneTask = isStereometry
            ? _stereoGen.GenerateSceneAsync(question, context)
            : Task.FromResult<string?>(null);

        var result = await resultTask;

        var (narrationSystemPrompt, narrationUserContent) = BuildNarrationPrompt(result, question, languageInstruction);

        if (narrationSystemPrompt == null)
        {
            // Total STEM failure -- degrade to the generic path. sceneTask is deliberately
            // left un-awaited: GenerateSceneAsync catches every exception internally and
            // returns null/logs on failure, so there's no unobserved-exception risk, and
            // its result would be discarded anyway.
            await foreach (var token in AskGenericStreamAsync(question, context, languageName))
                yield return token;
            yield break;
        }

        // Stream narration to the client as soon as Qwen resolves -- do not wait on
        // sceneTask first. This is the actual latency fix: previously the scene await sat
        // here before narration ever started, so a slow (possibly 3x-retried) scene call
        // delayed the first token the student ever saw.
        await foreach (var token in _chat.StreamTokensAsync(question, narrationUserContent, narrationSystemPrompt))
            yield return token;

        // Only now, after narration has fully streamed, do we need the scene. By this
        // point sceneTask has had the whole narration-streaming window to finish on its
        // own, so this await is often near-instant.
        var scene = await sceneTask;
        if (scene != null)
            yield return $"\n<STEREO>{scene}</STEREO>";
    }

    // Direct-answer mode (Llm:StemDirectAnswer=true, the default): Qwen reasons AND writes the
    // final student-facing answer itself, skipping the Stage 2 BgGPT narration hand-off that
    // AskStemStreamAsync above uses. Same stereo-scene concurrency pattern as above (kicked off
    // early, only awaited after the answer has fully streamed). Because the answer is a live
    // stream rather than an already-awaited result, "did the whole thing fail" has to be
    // checked by pulling the first item manually before yielding anything — C# disallows
    // `yield return` inside a try block that has a catch clause, so the first MoveNextAsync is
    // done in its own try/catch, and only the rest of the iteration is a try/finally.
    private async IAsyncEnumerable<string> AskStemDirectStreamAsync(string question, string context, string? languageName)
    {
        var languageInstruction = languageName is not null
            ? $"Respond entirely in {languageName}."
            : "Respond in the same language the student used in their question.";

        var isStereometry = StereometryDetector.IsStereometryQuestion(question);
        var sceneTask = isStereometry
            ? _stereoGen.GenerateSceneAsync(question, context)
            : Task.FromResult<string?>(null);

        var enumerator = _stemPipeline.StreamDirectAnswerAsync(question, context, languageInstruction).GetAsyncEnumerator();
        bool hasFirst = false;
        bool failedBeforeFirstToken = false;
        try
        {
            hasFirst = await enumerator.MoveNextAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qwen direct-answer stream failed before any content for {Question}; degrading to generic path.", question);
            failedBeforeFirstToken = true;
        }

        if (failedBeforeFirstToken || !hasFirst)
        {
            if (!failedBeforeFirstToken)
                _logger.LogWarning("Qwen direct-answer stream produced no content for {Question}; degrading to generic path.", question);
            await foreach (var token in AskGenericStreamAsync(question, context, languageName))
                yield return token;
            yield break;
        }

        // A STEM-path failure must never reach the student as an error, same principle as
        // AskStemStreamAsync above -- but once the first token has already streamed, a
        // mid-stream failure here is not caught, matching the existing risk profile of every
        // other streaming call in this codebase (AskGenericStreamAsync/AskStemStreamAsync's
        // narration call have no mid-stream fallback either).
        yield return enumerator.Current;
        try
        {
            while (await enumerator.MoveNextAsync())
                yield return enumerator.Current;
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        var scene = await sceneTask;
        if (scene != null)
            yield return $"\n<STEREO>{scene}</STEREO>";
    }

    // Non-streaming diagnostic variant of the STEM pipeline, used only by the CLI test
    // harness (dotnet run -- test-stem-pipeline) so it can log the complete Qwen JSON/prose
    // alongside BgGPT's full narrated answer for a question. Uses _chat.OneShotAsync (fresh
    // history each call) rather than the streaming/history-appending path, so running many
    // unrelated fixture questions in one process doesn't contaminate each other's context.
    // Not used by any HTTP endpoint.
    //
    // Reasoning is null when Llm:StemDirectAnswer routes this call through direct mode -- there
    // is no Stage-1/Stage-2 split to report in that case, only the final Narration text.
    public async Task<(StemReasoningResult? Reasoning, string Narration)> AskStemDiagnosticAsync(string question)
    {
        question = InputSanitizer.SanitizeUserInput(question, maxLength: 2000);
        var context = await GetContextAsync(question);
        var languageName = _languageDetector.DetectLanguageName(question);
        var languageInstruction = languageName is not null
            ? $"Respond entirely in {languageName}."
            : "Respond in the same language the student used in their question.";

        if (_stemOptions.DirectAnswer)
        {
            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in _stemPipeline.StreamDirectAnswerAsync(question, context, languageInstruction))
                sb.Append(chunk);
            return (null, sb.Length > 0 ? sb.ToString() : "[Qwen direct-answer stream produced no content.]");
        }

        var result = await _stemPipeline.SolveAsync(question, context);
        var (narrationSystemPrompt, narrationUserContent) = BuildNarrationPrompt(result, question, languageInstruction);

        if (narrationSystemPrompt == null)
            return (result, "[STEM pipeline failed entirely — would degrade to the generic answer path.]");

        var narration = await _chat.OneShotAsync(narrationSystemPrompt, narrationUserContent!);
        return (result, narration);
    }

    // Shared by AskStemStreamAsync and AskStemDiagnosticAsync: picks the structured or
    // fallback-prose narration prompt depending on what Stage 1 produced. Both outputs null
    // means the reasoning stage failed entirely — the caller must degrade to the generic path.
    private static (string? SystemPrompt, string? UserContent) BuildNarrationPrompt(
        StemReasoningResult result, string question, string languageInstruction)
    {
        if (result.Structured != null)
        {
            return (
                StemAnswerPipelineService.BuildStructuredNarrationSystemPrompt(result.Structured.QuestionType, languageInstruction),
                StemAnswerPipelineService.BuildNarrationUserContent(result.Structured, question));
        }

        if (result.FallbackProse != null)
        {
            return (
                StemAnswerPipelineService.BuildFallbackNarrationSystemPrompt(languageInstruction),
                StemAnswerPipelineService.BuildFallbackNarrationUserContent(result.FallbackProse, question));
        }

        return (null, null);
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
