namespace NetBootloader.App.Localization;

/// <summary>
/// One entry in the language picker: a language, its name written in itself (so a
/// user can find their language regardless of what language the UI is currently in),
/// and a flag emoji shown both as the picker's compact closed-state display and next
/// to the name in the open dropdown.
/// </summary>
public sealed record LanguageOption(AppLanguage Value, string DisplayName, string Flag);
