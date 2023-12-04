using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using System.Net.Http.Headers;

using FineTuningApp.Models;

async Task<string> UploadFile(HttpClient client, string folder, string dataset, string purpose)
{
    var file = Path.Combine(folder, dataset);
    using var fs = File.OpenRead(file);
    StreamContent fileContent = new(fs);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
    {
        Name = "file",
        FileName = dataset
    };

    using MultipartFormDataContent formData = new();
    formData.Add(new StringContent(purpose), "purpose");
    formData.Add(fileContent);

    var response = await client.PostAsync("openai/files?api-version=2023-10-01-preview", formData);
    if (response.IsSuccessStatusCode)
    {
        var data = await response.Content.ReadFromJsonAsync<FileUploadResponse>();
        return data.id;
    }

    return string.Empty;
}

async Task<string> SubmitTrainingJob(HttpClient client, string trainingFileId, string validationFileId)
{
    TrainingRequestModel trainingRequestModel = new()
    {
        model = "gpt-35-turbo-0613",
        training_file = trainingFileId,
        validation_file = validationFileId,
    };

    var requestBody = JsonSerializer.Serialize(trainingRequestModel);
    StringContent content = new(requestBody, Encoding.UTF8, "application/json");

    var response = await client.PostAsync("openai/fine_tuning/jobs?api-version=2023-10-01-preview", content);

    if (response.IsSuccessStatusCode)
    {
        var data = await response.Content.ReadFromJsonAsync<TrainingResponseModel>();
        return data.id;
    }

    return string.Empty;
}

async Task<TrainingResponseModel> CheckTrainingJobStatus(HttpClient client, string trainingJobId)
{
    var response = await client.GetAsync($"openai/fine_tuning/jobs/{trainingJobId}?api-version=2023-10-01-preview");

    if (response.IsSuccessStatusCode)
    {
        var data = await response.Content.ReadFromJsonAsync<TrainingResponseModel>();
        return data;
    }

    return null;
}

async Task<string> DeployModel(HttpClient client, string modelName, string deploymentName, string token, string subscriptionId, string resourceGroup, string resourceName)
{
    var requestUrl = $"subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.CognitiveServices/accounts/{resourceName}/deployments/{deploymentName}?api-version=2023-10-01-preview";
    var deploymentRequestModel = new DeploymentRequestModel()
    {
        sku = new(),
        properties = new() { model = new() { name = modelName } }
    };

    var requestBody = JsonSerializer.Serialize(deploymentRequestModel);
    StringContent content = new(requestBody, Encoding.UTF8, "application/json");

    var response = await client.PutAsync(requestUrl, content);

    if (response.IsSuccessStatusCode)
    {
        var data = await response.Content.ReadFromJsonAsync<DeploymentResponseModel>();
        return data.id;
    }

    return string.Empty;
}

async Task<string> CheckDeploymentJobStatus(HttpClient client, string id)
{
    var response = await client.GetAsync($"{id}?api-version=2023-10-01-preview");

    if (response.IsSuccessStatusCode)
    {
        var data = await response.Content.ReadFromJsonAsync<DeploymentJobResponseModel>();
        return data.properties.provisioningState;
    }

    return string.Empty;
}

async Task<string> GetChatCompletion(HttpClient client, string deploymentName, string systemMessage, string userInput)
{
    ChatCompletionRequest chatCompletion = new()
    {
        messages =
        [
            new() { role = "system", content = systemMessage },
            new() { role = "user", content = userInput }
        ]
    };

    var requestBody = JsonSerializer.Serialize(chatCompletion);
    StringContent content = new StringContent(requestBody, Encoding.UTF8, "application/json");

    var response = await client.PostAsync($"openai/deployments/{deploymentName}/chat/completions?api-version=2023-10-01-preview", content);

    if (response.IsSuccessStatusCode)
    {
        var data = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>();
        return data.choices.First().message.content;
    }

    return string.Empty;
}

HttpClient client = new();
client.BaseAddress = new("your-endpoint");
client.DefaultRequestHeaders.Add("api-key", "your-api-key");

var filesFolder = "Files";
var trainingDataset = "recipe_training.jsonl";
var validationDataset = "recipe_validation.jsonl";
var purpose = "fine-tune";

var line = new String('-', 20);
Console.WriteLine(line);
Console.WriteLine("***** UPLOADING FILES *****");
var trainingDsId = await UploadFile(client, filesFolder, trainingDataset, purpose);
Console.WriteLine("Training dataset: " + trainingDsId);

var validationDsId = await UploadFile(client, filesFolder, validationDataset, purpose);
Console.WriteLine("Validation dataset: " + validationDsId);
Console.WriteLine(line);

await Task.Delay(10000);

Console.WriteLine("***** TRAINING CUSTOM MODEL *****");
var trainingJobId = await SubmitTrainingJob(client, trainingDsId, validationDsId);
Console.WriteLine("Training Job Id: " + trainingJobId);

string? fineTunedModelName;
var status = string.Empty;

do
{
    var trainingStatus = await CheckTrainingJobStatus(client, trainingJobId);
    Console.WriteLine(DateTime.Now.ToShortTimeString() + ". Training Job Status: " + trainingStatus.status);
    fineTunedModelName = trainingStatus.fine_tuned_model;
    status = trainingStatus.status;
    await Task.Delay(5 * 60 * 1000);
} while (status != "succeeded");

Console.WriteLine("Fine-tuned model name: " + fineTunedModelName);
Console.WriteLine(line);

var deploymentName = "ingredients_extractor";
string subscriptionId = "your-subscription-id";
string resourceGroup = "your-resource-group";
string resourceName = "your-resource-name";
Console.WriteLine("***** ENTER THE TOKEN *****");
string token = Console.ReadLine();

HttpClient clientManagement = new();
clientManagement.BaseAddress = new("https://management.azure.com/");
clientManagement.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

Console.WriteLine("***** DEPLOYING CUSTOM MODEL *****");
var deploymentJobId = await DeployModel(clientManagement, fineTunedModelName, deploymentName, token, subscriptionId, resourceGroup, resourceName);
Console.WriteLine("Deployment ID: " + deploymentJobId);

var deploymentStatus = string.Empty;

do
{
    deploymentStatus = await CheckDeploymentJobStatus(clientManagement, deploymentJobId);
    Console.WriteLine(DateTime.Now.ToShortTimeString() + ". Deployment Job Status: " + deploymentStatus);
    await Task.Delay(5 * 60 * 1000);
} while (deploymentStatus != "Succeeded");
Console.WriteLine(line);

Console.WriteLine("***** USING CUSTOM MODEL *****");
var systemMessage = "You are a helpful recipe assistant. You are to extract the generic ingredients from each of the recipes provided";
var userMessage = "Title: Pancakes\n\nIngredients: [\"1 c. flour\", \"1 tsp. soda\", \"1 tsp. salt\", \"1 Tbsp. sugar\", \"1 egg\", \"3 Tbsp. margarine, melted\", \"1 c. buttermilk\"]\n\nGeneric ingredients: ";
Console.WriteLine("User Message: " + userMessage);

var inference = await GetChatCompletion(client, deploymentName, systemMessage, userMessage);
Console.WriteLine("AI Message: " + inference);
Console.WriteLine(line);