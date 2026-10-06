using System.Text;
using PrinterHub.Core.Model;
using PrinterHub.Core.Util;

namespace PrinterHub.Languages.Generic;

/// <summary>
/// Gửi nguyên byte (file .prn, ZPL/SBPL/EZPL có sẵn, ESC/P, ESC/POS, PCL...). Không render nhãn, không đọc trạng thái.
/// </summary>
public sealed class RawLanguage(IReadOnlyDictionary<string, string>? options = null) : Languages.LanguageBase(options)
{
    public override string Name => "RAW";
    public override bool CanRenderLabel => false;
    public override byte[] Render(LabelDocument document) => throw new NotSupportedException("RAW không render nhãn.");
    public override byte[]? BuildStatusRequest() => null;
    public override bool TryParseStatus(ReadOnlySpan<byte> response, out PrinterStatus status)
    {
        status = PrinterStatus.Unknown();
        return false;
    }
}

/// <summary>
/// HP PJL (Printer Job Language) — bọc dữ liệu PDF/PCL/PostScript cho máy in văn phòng
/// (FUJIFILM Apeos/DocuPrint, HP, Ricoh...). Gửi qua port 9100 hoặc LPR.
/// Trạng thái: @PJL INFO STATUS → CODE=10001 (Ready).
///
/// Tuỳ chọn: <c>language</c> (PDF, PCL, POSTSCRIPT... mặc định lấy từ PrintJob.RawFormat),
/// <c>duplex</c> (true/false), <c>paper</c> (A4, LETTER...).
/// ⚠ Mức hỗ trợ PJL trên máy FUJIFILM cần kiểm tra theo model.
/// </summary>
public sealed class PjlLanguage(IReadOnlyDictionary<string, string>? options = null) : Languages.LanguageBase(options)
{
    private const string Uel = "\x1B%-12345X";

    public override string Name => "PJL";
    public override bool CanRenderLabel => false;
    public override byte[] Render(LabelDocument document) => throw new NotSupportedException("PJL không render nhãn; gửi PDF/PCL raw.");

    public override byte[] WrapRaw(byte[] data, PrintJob job)
    {
        string lang = (job.RawFormat ?? Opt("language", "PDF")).ToUpperInvariant();
        var w = new CommandWriter(TextEncodings.Utf8);
        w.Ascii(Uel).Line("@PJL");
        w.Line($"@PJL JOB NAME=\"{job.Name.Replace("\"", "'")}\"");
        w.Line($"@PJL SET COPIES={I(Math.Max(1, job.Copies))}");
        if (OptOrNull("duplex") is { } dx) w.Line($"@PJL SET DUPLEX={(bool.Parse(dx) ? "ON" : "OFF")}");
        if (OptOrNull("paper") is { } paper) w.Line($"@PJL SET PAPER={paper.ToUpperInvariant()}");
        w.Line($"@PJL ENTER LANGUAGE={lang}");
        w.Bytes(data);
        w.Ascii(Uel).Line("@PJL EOJ");
        w.Ascii(Uel);
        return w.ToArray();
    }

    public override byte[]? BuildStatusRequest() => Encoding.ASCII.GetBytes($"{Uel}@PJL INFO STATUS\r\n{Uel}");

    public override bool TryParseStatus(ReadOnlySpan<byte> response, out PrinterStatus status)
    {
        status = PrinterStatus.Unknown();
        string s = Encoding.ASCII.GetString(response);
        int ff = s.IndexOf('\f');
        if (!s.Contains("CODE=") || ff < 0) return false;

        int codeStart = s.IndexOf("CODE=", StringComparison.Ordinal) + 5;
        string code = new(s[codeStart..].TakeWhile(char.IsDigit).ToArray());
        bool online = !s.Contains("ONLINE=FALSE", StringComparison.OrdinalIgnoreCase);
        int display = s.IndexOf("DISPLAY=", StringComparison.Ordinal);
        string? msg = display >= 0 ? s[(display + 8)..].Split('\r', '\n')[0].Trim('"') : null;

        PrinterState state = code switch
        {
            "10001" or "10002" => PrinterState.Ready,
            "10023" or "10024" => PrinterState.Printing,
            "10003" or "10004" or "10005" or "10006" => PrinterState.Busy,
            _ when code.StartsWith("4", StringComparison.Ordinal) => PrinterState.Error,
            _ => PrinterState.Unknown,
        };
        if (!online && state != PrinterState.Error) state = PrinterState.Offline;
        var errors = PrinterErrors.None;
        if (code.StartsWith("41", StringComparison.Ordinal)) errors |= PrinterErrors.PaperOut;
        else if (code.StartsWith("42", StringComparison.Ordinal)) errors |= PrinterErrors.PaperJam;
        else if (code.StartsWith("4", StringComparison.Ordinal)) errors |= PrinterErrors.Other;

        status = new PrinterStatus { State = state, Errors = errors, RawCode = code, Message = msg, RawResponse = response.ToArray() };
        return true;
    }
}
