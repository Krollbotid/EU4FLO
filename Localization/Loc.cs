using System.Text.Json;

namespace EU4FLO.Localization;

public static class Loc
{
    private static string _dir = "locales";
    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _fallback = new();

    public static string CurrentLanguage { get; private set; } = "en";
    public static event Action? LanguageChanged;

    public static void Initialize(string? preferredLanguage = null)
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "locales");
        if (!Directory.Exists(_dir))
            _dir = Path.Combine(Environment.CurrentDirectory, "locales");

        _fallback = ReadFile("en");

        var lang = preferredLanguage
                   ?? DetectSystemLanguage()
                   ?? "en";
        SetLanguage(lang);
    }

    public static IEnumerable<(string Code, string Name)> AvailableLanguages()
    {
        if (!Directory.Exists(_dir)) yield break;

        foreach (var path in Directory.GetFiles(_dir, "*.json"))
        {
            var code = Path.GetFileNameWithoutExtension(path);
            var dict = ReadFile(code);
            var name = dict.TryGetValue("_language_name", out var n) ? n : code;
            yield return (code, name);
        }
    }

    public static void SetLanguage(string code)
    {
        var dict = ReadFile(code);
        if (dict.Count == 0)
        {
            // fallback
            code = "en";
            dict = new Dictionary<string, string>(_fallback);
        }
        _strings = dict;
        CurrentLanguage = code;
        LanguageChanged?.Invoke();
    }

    public static string T(string key)
        => _strings.TryGetValue(key, out var v) ? v
         : _fallback.TryGetValue(key, out var f) ? f
         : key;

    public static string T(string key, params object[] args)
        => string.Format(T(key), args);

    private static Dictionary<string, string> ReadFile(string code)
    {
        try
        {
            var path = Path.Combine(_dir, code + ".json");
            if (!File.Exists(path)) return new();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new();
        }
        catch { return new(); }
    }

    private static string? DetectSystemLanguage()
    {
        var c = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return string.IsNullOrEmpty(c) ? null : c;
    }
}