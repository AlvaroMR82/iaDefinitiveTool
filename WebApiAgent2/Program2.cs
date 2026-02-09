using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel; // <-- Agrega este using
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

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

//Programador
builder.AddAIAgent(
    "Programador",
    "Responde como lo haria un programador informático"
);

// Meteorólogo/Editor: añadimos las tools para acceder a C:\reafile
builder.AddAIAgent("Meteorologo", (sp, key) =>
    new ChatClientAgent(
        chatClient.AsIChatClient(), // <-- Conversión explícita a IChatClient
        name: key,
        instructions:
            "Eres un ayudate ejemplar para consultar el tiempo",
        tools: new[] { AIFunctionFactory.Create(FormatStory), AIFunctionFactory.Create(GetWeather), AIFunctionFactory.Create(ListReaFile), AIFunctionFactory.Create(ReadFileFromReaFile) }

    )
);

// Workflow: writer → editor
builder.AddWorkflow("publisher", (sp, key) =>
    AgentWorkflowBuilder.BuildSequential(
        workflowName: key,
        sp.GetRequiredKeyedService<AIAgent>("Meteorologo"),
        sp.GetRequiredKeyedService<AIAgent>("Programador")
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
    HELPERS PARA SUBIR ARCHIVOS
    El error "The provided URI is not a data URI" ocurre cuando
    la API espera un data URI (data:<mime>;base64,...) y se
    le pasa una ruta local o una URL normal.
===========================================================*/

static string ConvertFileToDataUri(string filePath, string mediaType = "application/pdf")
{
    // Lee el archivo y lo codifica como base64, formando un data URI
    byte[] bytes = File.ReadAllBytes(filePath);
    string base64 = Convert.ToBase64String(bytes);
    return $"data:{mediaType};base64,{base64}";
}

/*===========================================================
    TOOLS PARA C:\reafile
    - Seguridad: evita directory traversal.
    - Limita tamaño inline para respuestas base64.
===========================================================*/

[Description("Lista los ficheros disponibles en C:\\readfile.")]
static string ListReaFile()
{
    var baseDir = Path.GetFullPath(@"C:\readfile");
    if (!Directory.Exists(baseDir))
        return "El directorio C:\\readfile no existe.";

    try
    {
        var files = Directory.GetFiles(baseDir).Select(Path.GetFileName).ToArray();
        return files.Length == 0 ? "No hay ficheros en C:\\readfile." : string.Join("\n", files);
    }
    catch (Exception ex)
    {
        return $"Error al listar C:\\readfile: {ex.Message}";
    }
}

[Description("Lee un fichero dentro de C:\\readfile. Devuelve texto para ficheros de texto o Data URI para binarios.")]
static string ReadFileFromReaFile([Description("Nombre del fichero")] string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName))
        return "Se debe proporcionar el nombre del fichero.";

    var baseDir = Path.GetFullPath(@"C:\readfile");
    var candidate = Path.GetFullPath(Path.Combine(baseDir, fileName));

    // Evitar traversal fuera del directorio
    if (!candidate.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        return "Acceso denegado.";

    if (!File.Exists(candidate))
        return "Fichero no encontrado.";

    try
    {
        var ext = Path.GetExtension(candidate).ToLowerInvariant();
        if (ext == ".txt" || ext == ".md" || ext == ".json" || ext == ".csv")
        {
            var txt = File.ReadAllText(candidate);
            // Limitar tamaño devuelto para evitar respuestas gigantes
            return txt.Length > 200_000 ? txt.Substring(0, 200_000) + "\n... (truncado)" : txt;
        }

        // Para binarios devolvemos data URI (limitado)
        var bytes = File.ReadAllBytes(candidate);
        var media = ext == ".pdf" ? "application/pdf" : "application/octet-stream";
        var base64 = Convert.ToBase64String(bytes);

        // Limitar tamaño devuelto inline a 5 MB base64 (~3.75 MB binario)
        const int maxInlineBase64 = 5 * 1024 * 1024;
        if (base64.Length > maxInlineBase64)
            return "Fichero demasiado grande para devolver inline. Indica una URL o reduce el tamaño.";

        return $"data:{media};base64,{base64}";
    }
    catch (Exception ex)
    {
        return $"Error al leer fichero: {ex.Message}";
    }
}

/*===========================================================
    TOOL
===========================================================*/

[Description("Formats the story for publication, revealing its title.")]
string FormatStory(string title, string story) => $"""
**Title**: {title}

{story}
""";


//[Description("Get the weather for a given location.")]
//static string GetWeather([Description("The location to get the weather for.")] string location)
//	=> $"The weather in {location} is cloudy with a high of 15°C.";



[Description("Get the weather for a given location.")]
 static async Task<string> GetWeather(
	[Description("The location to get the weather for.")] string location)
{
	string apiKey = "6f46de3d201c4934662dc7d53e82f875"; // <-- remplazalo
	using var http = new HttpClient();

	var url =
		$"https://api.openweathermap.org/data/2.5/weather?q={location}&appid={apiKey}&units=metric&lang=es";

	var data = await http.GetFromJsonAsync<WeatherResponse>(url);

	if (data == null)
		return $"No pude obtener el clima para {location}.";

	string desc = data.Weather[0].Description;
	float temp = data.Main.Temp;
	float humidity = data.Main.Humidity;

	return $"En {location}: {desc}, temperatura {temp}°C, humedad {humidity}%";
}

public class WeatherResponse
{
	public WeatherInfo[] Weather { get; set; }
	public MainInfo Main { get; set; }
}

public class WeatherInfo
{
	public string Description { get; set; }
}

public class MainInfo
{
	public float Temp { get; set; }
	public float Humidity { get; set; }
}
