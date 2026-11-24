using AiAssistant.Api.Voice;
using AiAssistant.Application.Common;
using AiAssistant.Application.Voice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AiAssistant.Api.Controllers;

[Authorize]
[Route("api/v1/voice")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VoiceController(VoiceService voice) : ApplicationController
{
    [HttpGet("capabilities")]
    public ActionResult<VoiceCapabilities> Capabilities() => voice.Capabilities();

    [HttpPost("transcriptions")]
    [EnableRateLimiting(VoiceSetup.RatePolicy)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(17 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = VoiceLimits.MaxUploadBytes, ValueLengthLimit = 64, ValueCountLimit = 4)]
    public async Task<IActionResult> Transcribe(CancellationToken cancellationToken)
    {
        try
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            if (form.Files.Count != 1 || form.Files.GetFile("audio") is not { } file || form["language"].Count > 1)
                return Invalid();
            await using var stream = file.OpenReadStream();
            return From(await voice.TranscribeAsync(CurrentUserId, stream, file.Length, form["language"], cancellationToken));
        }
        catch (InvalidDataException) { return Invalid(); }
    }

    private IActionResult Invalid() => Failure(new(ErrorKind.Validation, "voice_request", "Передайте одну аудиозапись и язык."));
}
