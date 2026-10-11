
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ShiftTimeTable.Web.Services;

public sealed class OpenAiScheduleReader
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OpenAiScheduleReader(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> ReadScheduleAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API anahtarı yapılandırılmamış.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        var isPdf = extension == ".pdf";
        var mimeType = isPdf
            ? "application/pdf"
            : extension == ".png"
                ? "image/png"
                : "image/jpeg";

        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);

        var base64 = Convert.ToBase64String(memoryStream.ToArray());

        object fileContent = isPdf
            ? new
            {
                type = "input_file",
                filename = "schedule.pdf",
                file_data = $"data:{mimeType};base64,{base64}"
            }
            : new
            {
                type = "input_image",
                image_url = $"data:{mimeType};base64,{base64}",
                detail = "high"
            };

        var schema = new
        {
            type = "object",
            properties = new
            {
                employees = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            shifts = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        day = new
                                        {
                                            type = "string",
                                            @enum = new[]
                                            {
                                                "Monday",
                                                "Tuesday",
                                                "Wednesday",
                                                "Thursday",
                                                "Friday",
                                                "Saturday",
                                                "Sunday"
                                            }
                                        },
                                        startTime = new
                                        {
                                            anyOf = new object[]
                                            {
                                                new { type = "string" },
                                                new { type = "null" }
                                            }
                                        },
                                        endTime = new
                                        {
                                            anyOf = new object[]
                                            {
                                                new { type = "string" },
                                                new { type = "null" }
                                            }
                                        }
                                    },
                                    required = new[]
                                    {
                                        "day", "startTime", "endTime"
                                    },
                                    additionalProperties = false
                                }
                            }
                        },
                        required = new[] { "name", "shifts" },
                        additionalProperties = false
                    }
                }
            },
            required = new[] { "employees" },
            additionalProperties = false
        };

        var prompt = """
            Bu dosya bir mağazanın haftalık vardiya çizelgesidir.
            Görseli veya PDF sayfalarını dikkatlice incele.

            Personel adlarını ve günlere karşılık gelen vardiya
            başlangıç/bitiş saatlerini çıkar.

            Kurallar:
            - Yalnızca çizelgede açıkça görülen bilgileri kullan.
            - İzinli veya vardiyası olmayan günler için vardiya üretme.
            - Saat net okunamıyorsa ilgili saat alanını null yap.
            - Saatleri 24 saatlik HH:mm formatında yaz.
            - Gün adlarını İngilizce olarak Monday-Sunday arasında döndür.
            - Aynı kişiye ait vardiyaları aynı personel altında birleştir.
            - Vardiya saatleri dışında mola saatleri uydurma.
            - Başlıkları personel adı olarak değerlendirme.
            - Sadece istenen şemaya uyan sonucu döndür.
            """;

        var requestBody = new
        {
            model = _configuration["OpenAI:Model"] ?? "gpt-4.1",
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_text",
                            text = prompt
                        },
                        fileContent
                    }
                }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "shift_schedule",
                    strict = true,
                    schema
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/responses");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // API anahtarını veya hassas başlıkları hata mesajına ekleme.
            throw new HttpRequestException(
                $"OpenAI isteği başarısız oldu. HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(responseText);

        if (!document.RootElement.TryGetProperty(
                "output", out var output))
        {
            throw new InvalidOperationException(
                "API yanıtında çıktı bulunamadı.");
        }

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var itemType) ||
                itemType.GetString() != "message")
            {
                continue;
            }

            if (!item.TryGetProperty("content", out var content))
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var partType) &&
                    partType.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text))
                {
                    var json = text.GetString();

                    if (!string.IsNullOrWhiteSpace(json))
                        return json;
                }
            }
        }

        throw new InvalidOperationException(
            "Modelden okunabilir JSON çıktısı alınamadı.");
    }
}
