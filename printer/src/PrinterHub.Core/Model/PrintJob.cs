namespace PrinterHub.Core.Model;

/// <summary>Một yêu cầu in: hoặc nhãn trung lập, hoặc dữ liệu raw có sẵn.</summary>
public sealed record PrintJob
{
    public string Name { get; init; } = $"Job-{DateTime.Now:yyyyMMdd-HHmmss}";
    public LabelDocument? Label { get; init; }
    public byte[]? RawData { get; init; }
    /// <summary>Định dạng raw (cho PJL: "PDF", "PCL", "POSTSCRIPT"...).</summary>
    public string? RawFormat { get; init; }
    public int Copies { get; init; } = 1;
    /// <summary>Kiểm tra trạng thái trước khi gửi (nếu route hai chiều).</summary>
    public bool CheckStatusBeforePrint { get; init; } = true;

    public static PrintJob FromLabel(LabelDocument doc, string? name = null) =>
        new() { Label = doc, Name = name ?? doc.JobName ?? $"Label-{DateTime.Now:HHmmss}", Copies = doc.Copies };

    public static PrintJob FromRaw(byte[] data, string? name = null, string? format = null) =>
        new() { RawData = data, Name = name ?? $"Raw-{DateTime.Now:HHmmss}", RawFormat = format };
}

public sealed record PrintResult
{
    public bool Success { get; init; }
    public string? Route { get; init; }
    public int BytesSent { get; init; }
    public int Attempts { get; init; }
    public PrinterStatus? StatusBefore { get; init; }
    public PrinterStatus? StatusAfter { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string? Error { get; init; }

    public override string ToString() => Success
        ? $"OK via {Route}: {BytesSent} bytes, {Attempts} attempt(s), {Elapsed.TotalMilliseconds:F0} ms"
        : $"FAILED ({Attempts} attempt(s)): {Error}";
}
