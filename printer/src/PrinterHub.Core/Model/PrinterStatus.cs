namespace PrinterHub.Core.Model;

public enum PrinterState
{
    Unknown,
    /// <summary>Online, sẵn sàng nhận dữ liệu.</summary>
    Ready,
    /// <summary>Đang nhận/phân tích dữ liệu.</summary>
    Busy,
    /// <summary>Đang in.</summary>
    Printing,
    /// <summary>Chờ bóc nhãn / chờ cắt.</summary>
    WaitingForTakeOut,
    /// <summary>Offline / pause.</summary>
    Offline,
    /// <summary>Lỗi (xem <see cref="PrinterStatus.Errors"/>).</summary>
    Error,
}

[Flags]
public enum PrinterErrors
{
    None = 0,
    PaperOut = 1 << 0,
    RibbonOut = 1 << 1,
    HeadOpen = 1 << 2,
    CoverOpen = 1 << 3,
    PaperJam = 1 << 4,
    BufferOverflow = 1 << 5,
    SensorError = 1 << 6,
    HeadError = 1 << 7,
    CutterError = 1 << 8,
    MediaError = 1 << 9,
    SyntaxError = 1 << 10,
    Temperature = 1 << 11,
    MemoryFull = 1 << 12,
    Other = 1 << 30,
}

[Flags]
public enum PrinterWarnings
{
    None = 0,
    RibbonNearEnd = 1 << 0,
    BufferNearFull = 1 << 1,
    PaperNearEnd = 1 << 2,
    Paused = 1 << 3,
}

/// <summary>Trạng thái máy in đã chuẩn hoá giữa các hãng.</summary>
public sealed record PrinterStatus
{
    public PrinterState State { get; init; } = PrinterState.Unknown;
    public PrinterErrors Errors { get; init; }
    public PrinterWarnings Warnings { get; init; }
    /// <summary>Số nhãn còn phải in (nếu máy in báo).</summary>
    public int? RemainingLabels { get; init; }
    public string? JobId { get; init; }
    public string? JobName { get; init; }
    /// <summary>Mã trạng thái gốc của hãng (vd SATO 'A', Godex "00").</summary>
    public string? RawCode { get; init; }
    public string? Message { get; init; }
    public byte[]? RawResponse { get; init; }

    public bool IsError => State == PrinterState.Error || Errors != PrinterErrors.None;
    public bool CanPrint => !IsError && State is PrinterState.Ready or PrinterState.Busy or PrinterState.Printing or PrinterState.WaitingForTakeOut;

    public static PrinterStatus Unknown(string? msg = null) => new() { State = PrinterState.Unknown, Message = msg };

    public override string ToString()
    {
        var parts = new List<string> { State.ToString() };
        if (Errors != PrinterErrors.None) parts.Add($"Errors={Errors}");
        if (Warnings != PrinterWarnings.None) parts.Add($"Warnings={Warnings}");
        if (RemainingLabels is { } r) parts.Add($"Remaining={r}");
        if (JobId is not null) parts.Add($"JobId={JobId}");
        if (RawCode is not null) parts.Add($"Code='{RawCode}'");
        if (Message is not null) parts.Add(Message);
        return string.Join(" | ", parts);
    }
}
