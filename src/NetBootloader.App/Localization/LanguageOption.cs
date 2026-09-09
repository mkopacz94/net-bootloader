namespace NetBootloader.App.Localization;

/// <summary>
/// One entry in the language picker: a language plus its name written in itself (so
/// a user can find their language regardless of what language the UI is currently in).
/// </summary>
public sealed record LanguageOption(AppLanguage Value, string DisplayName);
