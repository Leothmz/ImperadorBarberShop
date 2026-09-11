namespace ImperadorBarberShop.Application.Common;

/// <summary>
/// Canais de notificação ativos, guardados em <c>AppSettings["notifications:channels"]</c>
/// como lista separada por vírgula ("email", "whatsapp" ou "email,whatsapp").
/// </summary>
public static class NotificationChannels
{
    public const string SettingKey = "notifications:channels";
    public const string Email = "email";
    public const string WhatsApp = "whatsapp";

    /// <summary>Sem a chave vale só e-mail — o mesmo padrão semeado no boot.</summary>
    public static HashSet<string> Parse(string? raw)
        => (raw ?? Email)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
