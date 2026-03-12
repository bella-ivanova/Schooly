using System;
using Tesseract;
using System.IO;

namespace StudyAssistant.Services
{
    public class OCRService
    {
        private readonly string _tessDataPath;

        public OCRService(string tessDataPath = "tessdata")
        {
            _tessDataPath = tessDataPath;
        }

        // Extract text from an image file
        public string ExtractText(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new Exception($"Image file not found: {imagePath}");

            try
            {
                using var engine = new TesseractEngine(_tessDataPath, "eng", EngineMode.Default);
                using var img = Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);
                var text = page.GetText();
                return text.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR error: {ex.Message}");
                return "";
            }
        }
    }
}