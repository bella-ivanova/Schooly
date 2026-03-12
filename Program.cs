using StudyAssistant.Services;

Console.WriteLine("=== Study Assistant (GLM-5) ===");

// 🔹 Ask user for model (default: glm5)
Console.Write("Enter model name (default: glm5): ");
var modelInput = Console.ReadLine();
var model = string.IsNullOrWhiteSpace(modelInput) ? "glm5" : modelInput;

var chat = new OllamaChatService(model);

// 🔹 Ask user for system prompt
Console.WriteLine("\nEnter system prompt (leave empty for default):");
var systemPrompt = Console.ReadLine();

if (string.IsNullOrWhiteSpace(systemPrompt))
{
    systemPrompt = "You are a helpful school tutor and C# coding assistant. Explain step by step.";
}

chat.SetSystemPrompt(systemPrompt);

Console.WriteLine("\nType 'exit' to quit.\n");

// 🔹 Chat loop
while (true)
{
    Console.Write("\nYou: ");
    var input = Console.ReadLine();

    if (input?.ToLower() == "exit")
        break;

    Console.Write("AI: ");
    await chat.StreamMessageAsync(input ?? "");
}
