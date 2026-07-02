namespace ImperadorBarberShop.Domain.Entities;

public class AppSettings
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    private AppSettings() { }

    public static AppSettings Create(string key, string value) => new() { Key = key, Value = value };

    public void SetValue(string value) => Value = value;
}
