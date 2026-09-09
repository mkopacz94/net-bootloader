namespace NetBootloader.Core;

/// <summary>Stage of an in-progress firmware flash operation, for UI progress reporting.</summary>
public enum FlashStage
{
    Handshaking,
    Erasing,
    Writing,
    SelfVerifying,
    Resetting,
}

/// <summary>A progress update reported during <see cref="FirmwareFlasher.FlashAsync"/>.</summary>
public sealed record FlashProgressReport(FlashStage Stage, int BytesDone, int BytesTotal);
