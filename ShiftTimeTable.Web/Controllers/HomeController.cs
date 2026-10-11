using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShiftTimeTable.Web.Models;
using Microsoft.AspNetCore.Mvc;
using ShiftTimeTable.Web.Services;

namespace ShiftTimeTable.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ParseSchedule(
        IFormFile? file,
        [FromServices] OpenAiScheduleReader reader,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                error = "Lütfen bir dosya seç."
            });
        }

        const long maxFileSize = 10 * 1024 * 1024;

        if (file.Length > maxFileSize)
        {
            return BadRequest(new
            {
                error = "Dosya boyutu en fazla 10 MB olabilir."
            });
        }

        var extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        if (extension is not (".png" or ".jpg" or ".jpeg" or ".pdf"))
        {
            return BadRequest(new
            {
                error = "Yalnızca PNG, JPG, JPEG ve PDF kabul edilir."
            });
        }

        // Dosya uzantısına ek olarak gerçek dosya imzasını kontrol et.
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();

        var isPdf = bytes.Length >= 5 &&
            bytes[0] == 0x25 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x44 &&
            bytes[3] == 0x46 &&
            bytes[4] == 0x2D;

        var isPng = bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A;

        var isJpeg = bytes.Length >= 3 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xD8 &&
            bytes[2] == 0xFF;

        var signatureMatches = extension switch
        {
            ".pdf" => isPdf,
            ".png" => isPng,
            ".jpg" or ".jpeg" => isJpeg,
            _ => false
        };

        if (!signatureMatches)
        {
            return BadRequest(new
            {
                error = "Dosya içeriği uzantısıyla eşleşmiyor veya dosya geçersiz."
            });
        }

        try
        {
            // Dosyayı diske kaydetmeden API'ye gönder.
            await using var validatedStream = new MemoryStream(bytes);
            var validatedFile = new FormFile(
                validatedStream, 0, bytes.Length, "file", file.FileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = file.ContentType
            };

            var json = await reader.ReadScheduleAsync(
                validatedFile, cancellationToken);

            using var parsedJson = System.Text.Json.JsonDocument.Parse(json);

            return Content(
                parsedJson.RootElement.GetRawText(),
                "application/json");
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new
            {
                error = "Yapay zekâ servisine ulaşılamadı veya istek başarısız oldu. API ayarlarını kontrol et."
            });
        }
        catch (InvalidOperationException ex)
        {
            // Yapılandırma ve okunabilir çıktı hatalarını kontrollü döndür.
            return StatusCode(502, new
            {
                error = ex.Message
            });
        }
        catch (System.Text.Json.JsonException)
        {
            return StatusCode(502, new
            {
                error = "Yapay zekâ yanıtı geçerli JSON formatında değil."
            });
        }
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
