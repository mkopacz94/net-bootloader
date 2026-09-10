using System.ComponentModel;
using System.Globalization;

namespace NetBootloader.App.Localization;

/// <summary>
/// UI text for every language the app supports, as a singleton so XAML can bind to it
/// directly (<c>{Binding Source={x:Static loc:Strings.Instance}, Path=ConnectionHeader}</c>)
/// and every bound label updates live when <see cref="Language"/> changes - no app
/// restart, no satellite assemblies. Deliberately simple for an app this size rather
/// than full .resx/CultureInfo resource-manager infrastructure.
///
/// Static text is a plain property; text that takes parameters (a port name, a byte
/// count, an exception message) is a method instead, since <c>{Binding}</c> can't pass
/// arguments. Each key doubles as its own dictionary key via <c>nameof</c>, so a typo
/// in either place is a compile error, not a silent lookup miss at runtime.
/// </summary>
public sealed class Strings : INotifyPropertyChanged
{
    public static Strings Instance { get; } = new();

    private Strings()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private AppLanguage _language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
    {
        "pl" => AppLanguage.Polish,
        "fr" => AppLanguage.French,
        "de" => AppLanguage.German,
        _ => AppLanguage.English,
    };

    /// <summary>The language everything below is currently read from.</summary>
    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;

            // Empty/null property name is WPF's documented convention for "every
            // property on this object may have changed" - refreshes every binding
            // sourced from this singleton in one notification, static text and
            // in-flight parameterized text alike.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        }
    }

    // ----- Static UI text -----

    public string ConnectionHeader => T(nameof(ConnectionHeader));
    public string PortLabel => T(nameof(PortLabel));
    public string RefreshButton => T(nameof(RefreshButton));
    public string BaudRateLabel => T(nameof(BaudRateLabel));
    public string TimeoutLabel => T(nameof(TimeoutLabel));
    public string FirmwareHeader => T(nameof(FirmwareHeader));
    public string BrowseButton => T(nameof(BrowseButton));
    public string VerifyChecksumOption => T(nameof(VerifyChecksumOption));
    public string ResetAfterFlashOption => T(nameof(ResetAfterFlashOption));
    public string LogHeader => T(nameof(LogHeader));
    public string FlashButton => T(nameof(FlashButton));
    public string CancelButton => T(nameof(CancelButton));
    public string OkButton => T(nameof(OkButton));
    public string LanguagePickerTooltip => T(nameof(LanguagePickerTooltip));

    // ----- Status / log text -----

    public string StatusReady => T(nameof(StatusReady));
    public string StatusReadingBootAttrs => T(nameof(StatusReadingBootAttrs));
    public string StatusSelfVerifying => T(nameof(StatusSelfVerifying));
    public string StatusResetting => T(nameof(StatusResetting));
    public string StatusFlashComplete => T(nameof(StatusFlashComplete));
    public string StatusFlashCancelled => T(nameof(StatusFlashCancelled));
    public string StatusInvalidFirmwareFile => T(nameof(StatusInvalidFirmwareFile));
    public string StatusVerifyFailed => T(nameof(StatusVerifyFailed));
    public string StatusDecryptingPackage => T(nameof(StatusDecryptingPackage));

    public string StatusConnectingTo(string port) => Format(nameof(StatusConnectingTo), port);
    public string StatusErasing(string bytesDone, string bytesTotal) => Format(nameof(StatusErasing), bytesDone, bytesTotal);
    public string StatusWriting(string bytesDone, string bytesTotal) => Format(nameof(StatusWriting), bytesDone, bytesTotal);
    public string StatusError(string message) => Format(nameof(StatusError), message);
    public string StatusConnectionError(string message) => Format(nameof(StatusConnectionError), message);

    // ----- Messages -----

    public string MessageFlashingSuccessTitle => T(nameof(MessageFlashingSuccessTitle));
    public string MessageFlashingSuccessMessage => T(nameof(MessageFlashingSuccessMessage));

    // ----- Errors -----

    public string UnsupportedFirmwareFileType(string extension) => Format(nameof(UnsupportedFirmwareFileType), extension);
    public string ValidationRangeError(int min, int max) => Format(nameof(ValidationRangeError), min, max);

    public string InvalidFirmwareDialogTitle => T(nameof(InvalidFirmwareDialogTitle));
    public string InvalidFirmwareDialogMessage(string fileName, string details) =>
        Format(nameof(InvalidFirmwareDialogMessage), fileName, details);
    public string SelectedFileFallback => T(nameof(SelectedFileFallback));

    /// <summary>
    /// Generic error-dialog title, shared by every failure that doesn't need its own
    /// wording (unlike <see cref="InvalidFirmwareDialogTitle"/>) - the corresponding
    /// StatusXxx text (already a complete sentence) doubles as the dialog's message.
    /// </summary>
    public string ErrorDialogTitle => T(nameof(ErrorDialogTitle));

    // ----- Firmware file picker -----

    public string SelectFirmwareDialogTitle => T(nameof(SelectFirmwareDialogTitle));
    public string PackageFilesFilterLabel => T(nameof(PackageFilesFilterLabel));

    private string T(string key) => Translations[_language][key];

    private string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

    private static readonly Dictionary<AppLanguage, Dictionary<string, string>> Translations = new()
    {
        [AppLanguage.English] = new()
        {
            [nameof(ConnectionHeader)] = "Connection",
            [nameof(PortLabel)] = "Port:",
            [nameof(RefreshButton)] = "Refresh",
            [nameof(BaudRateLabel)] = "Baud rate:",
            [nameof(TimeoutLabel)] = "Timeout (s):",
            [nameof(FirmwareHeader)] = "Firmware",
            [nameof(BrowseButton)] = "Browse...",
            [nameof(VerifyChecksumOption)] = "Verify checksum after each write",
            [nameof(ResetAfterFlashOption)] = "Reset device after flashing",
            [nameof(LogHeader)] = "Log",
            [nameof(FlashButton)] = "Flash",
            [nameof(CancelButton)] = "Cancel",
            [nameof(OkButton)] = "OK",
            [nameof(LanguagePickerTooltip)] = "Language",

            [nameof(StatusReady)] = "Ready.",
            [nameof(StatusReadingBootAttrs)] = "Reading bootloader attributes...",
            [nameof(StatusSelfVerifying)] = "Running self-verification...",
            [nameof(StatusResetting)] = "Resetting device...",
            [nameof(StatusFlashComplete)] = "Flashing complete. Self-verify OK.",
            [nameof(StatusFlashCancelled)] = "Flashing cancelled.",
            [nameof(StatusInvalidFirmwareFile)] = "Error: the selected file isn't valid firmware.",
            [nameof(StatusVerifyFailed)] = "Error: flashing completed, but the bootloader reports no bootable application.",
            [nameof(StatusDecryptingPackage)] = "Decrypting firmware package in memory...",
            [nameof(StatusConnectingTo)] = "Connecting to {0}...",
            [nameof(StatusErasing)] = "Erasing program memory... {0} / {1}",
            [nameof(StatusWriting)] = "Writing firmware... {0} / {1}",
            [nameof(StatusError)] = "Error: {0}",
            [nameof(StatusConnectionError)] = "Connection error: {0}",

            [nameof(UnsupportedFirmwareFileType)] = "Unsupported firmware file type \"{0}\" - expected .tmfw.",
            [nameof(ValidationRangeError)] = "Enter a whole number from {0} to {1}.",
            [nameof(InvalidFirmwareDialogTitle)] = "Invalid firmware file",
            [nameof(InvalidFirmwareDialogMessage)] = "{0} doesn't look like valid firmware.\n\n{1}",
            [nameof(SelectedFileFallback)] = "The selected file",
            [nameof(ErrorDialogTitle)] = "Error",

            [nameof(MessageFlashingSuccessTitle)] = "Flashing finished",
            [nameof(MessageFlashingSuccessMessage)] = "Flashing the device memory completed succesfully.",

            [nameof(SelectFirmwareDialogTitle)] = "Select firmware image",
            [nameof(PackageFilesFilterLabel)] = "Encrypted firmware packages",
        },
        [AppLanguage.Polish] = new()
        {
            [nameof(ConnectionHeader)] = "Połączenie",
            [nameof(PortLabel)] = "Port:",
            [nameof(RefreshButton)] = "Odśwież",
            [nameof(BaudRateLabel)] = "Prędkość transmisji:",
            [nameof(TimeoutLabel)] = "Limit czasu (s):",
            [nameof(FirmwareHeader)] = "Oprogramowanie sprzętowe",
            [nameof(BrowseButton)] = "Przeglądaj...",
            [nameof(VerifyChecksumOption)] = "Weryfikuj sumę kontrolną po każdym zapisie",
            [nameof(ResetAfterFlashOption)] = "Zresetuj urządzenie po wgraniu",
            [nameof(LogHeader)] = "Dziennik",
            [nameof(FlashButton)] = "Wgraj",
            [nameof(CancelButton)] = "Anuluj",
            [nameof(OkButton)] = "OK",
            [nameof(LanguagePickerTooltip)] = "Język",

            [nameof(StatusReady)] = "Gotowy.",
            [nameof(StatusReadingBootAttrs)] = "Odczytywanie parametrów bootloadera...",
            [nameof(StatusSelfVerifying)] = "Trwa autoweryfikacja...",
            [nameof(StatusResetting)] = "Resetowanie urządzenia...",
            [nameof(StatusFlashComplete)] = "Wgrywanie zakończone. Autoweryfikacja OK.",
            [nameof(StatusFlashCancelled)] = "Wgrywanie anulowane.",
            [nameof(StatusInvalidFirmwareFile)] = "Błąd: wybrany plik nie jest prawidłowym oprogramowaniem sprzętowym.",
            [nameof(StatusVerifyFailed)] = "Błąd: wgrywanie zakończone, ale bootloader nie wykrywa aplikacji startowej.",
            [nameof(StatusDecryptingPackage)] = "Odszyfrowywanie pakietu oprogramowania w pamięci...",
            [nameof(StatusConnectingTo)] = "Łączenie z {0}...",
            [nameof(StatusErasing)] = "Czyszczenie pamięci programu... {0} / {1}",
            [nameof(StatusWriting)] = "Wgrywanie oprogramowania... {0} / {1}",
            [nameof(StatusError)] = "Błąd: {0}",
            [nameof(StatusConnectionError)] = "Błąd połączenia: {0}",

            [nameof(UnsupportedFirmwareFileType)] = "Nieobsługiwany typ pliku oprogramowania \"{0}\" - oczekiwano .tmfw.",
            [nameof(ValidationRangeError)] = "Wprowadź liczbę całkowitą od {0} do {1}.",
            [nameof(InvalidFirmwareDialogTitle)] = "Nieprawidłowy plik oprogramowania",
            [nameof(InvalidFirmwareDialogMessage)] = "{0} nie wygląda na prawidłowe oprogramowanie sprzętowe.\n\n{1}",
            [nameof(SelectedFileFallback)] = "Wybrany plik",
            [nameof(ErrorDialogTitle)] = "Błąd",

            [nameof(MessageFlashingSuccessTitle)] = "Wgrywanie zakończone",
            [nameof(MessageFlashingSuccessMessage)] = "Wgrywanie programu do pamięci urządzenia zakończone pomyślnie.",

            [nameof(SelectFirmwareDialogTitle)] = "Wybierz plik oprogramowania",
            [nameof(PackageFilesFilterLabel)] = "Zaszyfrowane pakiety oprogramowania",
        },
        [AppLanguage.French] = new()
        {
            [nameof(ConnectionHeader)] = "Connexion",
            [nameof(PortLabel)] = "Port :",
            [nameof(RefreshButton)] = "Actualiser",
            [nameof(BaudRateLabel)] = "Vitesse de transmission :",
            [nameof(TimeoutLabel)] = "Délai d'attente (s) :",
            [nameof(FirmwareHeader)] = "Micrologiciel",
            [nameof(BrowseButton)] = "Parcourir...",
            [nameof(VerifyChecksumOption)] = "Vérifier la somme de contrôle après chaque écriture",
            [nameof(ResetAfterFlashOption)] = "Réinitialiser l'appareil après le chargement",
            [nameof(LogHeader)] = "Journal",
            [nameof(FlashButton)] = "Charger",
            [nameof(CancelButton)] = "Annuler",
            [nameof(OkButton)] = "OK",
            [nameof(LanguagePickerTooltip)] = "Langue",

            [nameof(StatusReady)] = "Prêt.",
            [nameof(StatusReadingBootAttrs)] = "Lecture des paramètres du bootloader...",
            [nameof(StatusSelfVerifying)] = "Autovérification en cours...",
            [nameof(StatusResetting)] = "Réinitialisation de l'appareil...",
            [nameof(StatusFlashComplete)] = "Chargement terminé. Autovérification OK.",
            [nameof(StatusFlashCancelled)] = "Chargement annulé.",
            [nameof(StatusInvalidFirmwareFile)] = "Erreur : le fichier sélectionné n'est pas un micrologiciel valide.",
            [nameof(StatusVerifyFailed)] = "Erreur : le chargement est terminé, mais le bootloader ne détecte aucune application démarrable.",
            [nameof(StatusDecryptingPackage)] = "Déchiffrement du pack de micrologiciel en mémoire...",
            [nameof(StatusConnectingTo)] = "Connexion à {0}...",
            [nameof(StatusErasing)] = "Effacement de la mémoire programme... {0} / {1}",
            [nameof(StatusWriting)] = "Écriture du micrologiciel... {0} / {1}",
            [nameof(StatusError)] = "Erreur : {0}",
            [nameof(StatusConnectionError)] = "Erreur de connexion : {0}",

            [nameof(UnsupportedFirmwareFileType)] = "Type de fichier de micrologiciel non pris en charge « {0} » - .tmfw attendu.",
            [nameof(ValidationRangeError)] = "Entrez un nombre entier compris entre {0} et {1}.",
            [nameof(InvalidFirmwareDialogTitle)] = "Fichier de micrologiciel invalide",
            [nameof(InvalidFirmwareDialogMessage)] = "{0} ne ressemble pas à un micrologiciel valide.\n\n{1}",
            [nameof(SelectedFileFallback)] = "Le fichier sélectionné",
            [nameof(ErrorDialogTitle)] = "Erreur",

            [nameof(MessageFlashingSuccessTitle)] = "Chargement terminé",
            [nameof(MessageFlashingSuccessMessage)] = "Le chargement de la mémoire de l'appareil s'est terminé avec succès.",

            [nameof(SelectFirmwareDialogTitle)] = "Sélectionner l'image du micrologiciel",
            [nameof(PackageFilesFilterLabel)] = "Packs de micrologiciel chiffrés",
        },
        [AppLanguage.German] = new()
        {
            [nameof(ConnectionHeader)] = "Verbindung",
            [nameof(PortLabel)] = "Port:",
            [nameof(RefreshButton)] = "Aktualisieren",
            [nameof(BaudRateLabel)] = "Baudrate:",
            [nameof(TimeoutLabel)] = "Zeitlimit (s):",
            [nameof(FirmwareHeader)] = "Firmware",
            [nameof(BrowseButton)] = "Durchsuchen...",
            [nameof(VerifyChecksumOption)] = "Prüfsumme nach jedem Schreibvorgang überprüfen",
            [nameof(ResetAfterFlashOption)] = "Gerät nach dem Flashen zurücksetzen",
            [nameof(LogHeader)] = "Protokoll",
            [nameof(FlashButton)] = "Flashen",
            [nameof(CancelButton)] = "Abbrechen",
            [nameof(OkButton)] = "OK",
            [nameof(LanguagePickerTooltip)] = "Sprache",

            [nameof(StatusReady)] = "Bereit.",
            [nameof(StatusReadingBootAttrs)] = "Bootloader-Attribute werden gelesen...",
            [nameof(StatusSelfVerifying)] = "Selbstüberprüfung läuft...",
            [nameof(StatusResetting)] = "Gerät wird zurückgesetzt...",
            [nameof(StatusFlashComplete)] = "Flashen abgeschlossen. Selbstüberprüfung OK.",
            [nameof(StatusFlashCancelled)] = "Flashen abgebrochen.",
            [nameof(StatusInvalidFirmwareFile)] = "Fehler: Die ausgewählte Datei ist keine gültige Firmware.",
            [nameof(StatusVerifyFailed)] = "Fehler: Das Flashen wurde abgeschlossen, aber der Bootloader erkennt keine startfähige Anwendung.",
            [nameof(StatusDecryptingPackage)] = "Firmware-Paket wird im Speicher entschlüsselt...",
            [nameof(StatusConnectingTo)] = "Verbindung zu {0} wird hergestellt...",
            [nameof(StatusErasing)] = "Programmspeicher wird gelöscht... {0} / {1}",
            [nameof(StatusWriting)] = "Firmware wird geschrieben... {0} / {1}",
            [nameof(StatusError)] = "Fehler: {0}",
            [nameof(StatusConnectionError)] = "Verbindungsfehler: {0}",

            [nameof(UnsupportedFirmwareFileType)] = "Nicht unterstützter Firmware-Dateityp \"{0}\" - .tmfw erwartet.",
            [nameof(ValidationRangeError)] = "Geben Sie eine ganze Zahl von {0} bis {1} ein.",
            [nameof(InvalidFirmwareDialogTitle)] = "Ungültige Firmware-Datei",
            [nameof(InvalidFirmwareDialogMessage)] = "{0} sieht nicht wie gültige Firmware aus.\n\n{1}",
            [nameof(SelectedFileFallback)] = "Die ausgewählte Datei",
            [nameof(ErrorDialogTitle)] = "Fehler",

            [nameof(MessageFlashingSuccessTitle)] = "Flashen abgeschlossen",
            [nameof(MessageFlashingSuccessMessage)] = "Das Flashen des Gerätespeichers wurde erfolgreich abgeschlossen.",

            [nameof(SelectFirmwareDialogTitle)] = "Firmware-Image auswählen",
            [nameof(PackageFilesFilterLabel)] = "Verschlüsselte Firmware-Pakete",
        },
    };
}
