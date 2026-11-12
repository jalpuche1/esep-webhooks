
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

[assembly: LambdaSerializer(typeof(
    Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EsepWebhook;

public class Function
{
    private static readonly HttpClient Client = new HttpClient();

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return Response(400, "Missing request body");
            }

            using JsonDocument document = JsonDocument.Parse(request.Body);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("action", out var action) ||
                action.GetString() != "opened")
            {
                return Response(200, "Event ignored");
            }

            if (!root.TryGetProperty("issue", out var issue) ||
                !issue.TryGetProperty("html_url", out var urlElement))
            {
                return Response(400, "Missing issue URL");
            }

            string? issueUrl = urlElement.GetString();
            string? slackUrl = Environment.GetEnvironmentVariable("SLACK_URL");

            if (string.IsNullOrWhiteSpace(slackUrl))
            {
                context.Logger.LogLine("SLACK_URL is not configured.");
                return Response(500, "Slack not configured");
            }

            string message = $"Issue Created: {issueUrl}";

            string payload = JsonSerializer.Serialize(new
            {
                text = message
            });

            using var content = new StringContent(
                payload, Encoding.UTF8, "application/json");

            using HttpResponseMessage result =
                await Client.PostAsync(slackUrl, content);

            if (!result.IsSuccessStatusCode)
            {
                context.Logger.LogLine(
                    $"Slack returned status {result.StatusCode}");
                return Response(502, "Slack notification failed");
            }

            return Response(200, "Slack notification sent");
        }
        catch (JsonException)
        {
            return Response(400, "Invalid JSON");
        }
        catch (Exception ex)
        {
            context.Logger.LogLine(ex.Message);
            return Response(500, "Internal server error");
        }
    }

    private static APIGatewayProxyResponse Response(int status, string body)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = status,
            Body = body
        };
    }
}
