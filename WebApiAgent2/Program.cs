using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel; // <-- Agrega este using

var builder = WebApplication.CreateBuilder(args);

/*===========================================================
    CONFIGURACIÓN LM STUDIO
===========================================================*/

string lmUrl = builder.Configuration["LM_STUDIO_URL"] ?? "http://localhost:1234/v1";
string lmModel = builder.Configuration["LM_STUDIO_MODEL"] ?? "openai/gpt-oss-20b";
string lmKey = builder.Configuration["LM_STUDIO_KEY"] ?? "dummy_key"; // LMStudio no valida

// Crear cliente OpenAI apuntando a LM Studio
var openai = new OpenAIClient(
    new ApiKeyCredential(lmKey), // <-- Corrige aquí
    new OpenAIClientOptions
    {
        Endpoint = new Uri(lmUrl)
        // No hay propiedad ApiKey en OpenAIClientOptions
    }
);

// Crear ChatClient real para LM Studio
ChatClient chatClient = openai.GetChatClient(lmModel);

// Registrar ChatClient como servicio de Microsoft.Agents
builder.Services.AddChatClient(chatClient.AsIChatClient());

/*===========================================================
    AGENTES
===========================================================*/

// Writer
builder.AddAIAgent(
    "writer",
    "You write short stories (300 words or less) about the specified topic."
);

// Editor
builder.AddAIAgent("editor", (sp, key) =>
    new ChatClientAgent(
        chatClient.AsIChatClient(), // <-- Conversión explícita a IChatClient
        name: key,
        instructions:
            "You edit short stories to improve grammar and style, ensuring the stories are under 300 words. After editing, choose a title and format the story for publishing.",
        tools: new[] { AIFunctionFactory.Create(FormatStory) }
    )
);

// Workflow: writer → editor
builder.AddWorkflow("publisher", (sp, key) =>
    AgentWorkflowBuilder.BuildSequential(
        workflowName: key,
        sp.GetRequiredKeyedService<AIAgent>("writer"),
        sp.GetRequiredKeyedService<AIAgent>("editor")
    )
).AddAsAIAgent();

/*===========================================================
    DevUI
===========================================================*/

builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

/*===========================================================
    ASP.NET Core
===========================================================*/

var app = builder.Build();

app.UseHttpsRedirection();

app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (app.Environment.IsDevelopment())
{
    app.MapDevUI(); // http://localhost:xxxx/devui
}

app.Run();

/*===========================================================
    TOOL
===========================================================*/

[Description("Formats the story for publication, revealing its title.")]
string FormatStory(string title, string story) => $"""
**Title**: {title}

{story}
""";
