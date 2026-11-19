using Anthropic;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Okul.Modules.School.Application;

namespace Okul.Modules.School.Infrastructure;

/// <summary>
/// Öğretmen notundan veli mesajı taslağı. Claude kimliği varsa (<c>ANTHROPIC_API_KEY</c> veya <c>Anthropic:ApiKey</c>)
/// Claude Opus 5.5 yazar; yoksa ya da çağrı başarısızsa kurallı şablon. Taslak hiçbir zaman kendiliğinden gönderilmez (BR-9).
/// Çocuk verisi yalnızca taslak için gönderilir; model eğitiminde kullanılmaz (AI ilkeleri, API varsayılanı).
/// </summary>
internal sealed class MessageDrafter : IMessageDrafter
{
    private const string Model = "claude-opus-5-5";

    private const string SystemPrompt = """
        Sen bir özel okulun öğretmen asistanısın. Öğretmenin kısa, dağınık notunu veliye gidecek kibar, sıcak ve
        profesyonel bir Türkçe mesaja çevir.
        - Yalnızca nottaki ve verilen bilgideki olguları kullan; yeni olay, tarih veya teşhis uydurma.
        - Veliye "Sayın <veli adı>" diye hitap et, çocuktan ön adıyla bahset.
        - Olumsuz bir durumu suçlamadan, çözüm ve iş birliği önererek anlat; olumlu bir gözlemle dengele.
        - 3–6 cümle; madde işareti, başlık veya emoji kullanma.
        - Öğretmenin adıyla ve sınıfıyla imzala.
        - Yanıt olarak yalnızca mesaj metnini yaz.
        """;

    private readonly string? _apiKey;
    private readonly ILogger<MessageDrafter> _logger;

    public MessageDrafter(IConfiguration configuration, ILogger<MessageDrafter> logger)
    {
        _apiKey = configuration["Anthropic:ApiKey"] is { Length: > 0 } key ? key : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _logger = logger;
    }

    public async Task<DraftMessageResult> DraftAsync(MessageDraftInput input, CancellationToken cancellationToken)
    {
        var sources = new List<string>
        {
            $"Öğretmen notu: \"{input.TeacherNote}\"",
            $"Öğrenci: {input.StudentFullName}, {input.ClassName} (sınıf öğretmeni {input.TeacherName})",
            $"Veli: {input.GuardianName} ({input.GuardianRelation})",
            $"Yoklama (son 10 gün): {input.AbsenceCount} devamsız, {input.LateCount} geç",
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            try
            {
                var draft = await DraftWithClaudeAsync(input, cancellationToken);
                if (draft is not null)
                {
                    return new DraftMessageResult(draft, "Claude (Opus 5.5)", sources);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Claude taslağı alınamadı; şablona düşülüyor.");
            }
        }

        return new DraftMessageResult(Template(input), "Şablon (AI anahtarı yok)", sources);
    }

    private async Task<string?> DraftWithClaudeAsync(MessageDraftInput input, CancellationToken cancellationToken)
    {
        AnthropicClient client = new() { ApiKey = _apiKey! };
        var user = $"""
            Veli: {input.GuardianName} ({input.GuardianRelation})
            Öğrenci: {input.StudentFullName}, {input.ClassName}
            Öğretmen: {input.TeacherName}
            Son 10 gün yoklama: {input.AbsenceCount} devamsız, {input.LateCount} geç

            Öğretmenin notu:
            {input.TeacherNote}
            """;

        var response = await client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 2000,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            System = SystemPrompt,
            Messages = [new BetaUserMessageParam { Content = user }],
        }, cancellationToken);

        if (response.StopReason == BetaStopReason.Refusal)
        {
            return null;
        }

        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text)).Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>AI'sız taslak: notu kibar bir çerçeveye yerleştirir.</summary>
    internal static string Template(MessageDraftInput input)
    {
        var note = input.TeacherNote.Trim().TrimEnd('.');
        if (note.Length > 0)
        {
            note = char.ToUpper(note[0], System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + note[1..];
        }

        var attendance = input.AbsenceCount >= 2
            ? $" Ayrıca son iki haftada {input.AbsenceCount} gün devamsızlığı olduğunu da bilgilerinize sunmak isterim."
            : string.Empty;

        return $"""
            Sayın {input.GuardianName},

            {input.StudentFirstName} ile ilgili kısa bir bilgilendirme yapmak istedim: {note}.{attendance} Bu konuda okulla evin birlikte hareket etmesinin çok faydalı olacağına inanıyorum. Uygun olduğunuz bir zamanda görüşmekten memnuniyet duyarım.

            Saygılarımla,
            {input.TeacherName} — {input.ClassName} sınıf öğretmeni
            """;
    }
}
