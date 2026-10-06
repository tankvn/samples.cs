using System.Text.Json;
using PrinterHub.Core.Config;

namespace PrinterHub.Core.Model;

/// <summary>Nhãn mẫu dùng để thử nhanh mọi hãng/route.</summary>
public static class DemoLabels
{
    /// <summary>Nhãn 100 x 50 mm: khung, tiêu đề, Code128, QR, tiếng Nhật, tiếng Việt.</summary>
    public static LabelDocument Product(int dpi = 203)
    {
        var d = new LabelDocument { Dpi = dpi, JobName = "PRINTERHUB-DEMO" };
        d.Width = d.Mm(100);
        d.Height = d.Mm(50);
        d.Gap = d.Mm(3);
        d.Box(d.Mm(2), d.Mm(2), d.Mm(96), d.Mm(46), 3)
         .Text(d.Mm(5), d.Mm(4), "PRINTERHUB TEST", d.Mm(5))
         .Line(d.Mm(2), d.Mm(11), d.Mm(96))
         .Text(d.Mm(5), d.Mm(13), "PART: {PartNo}", d.Mm(3.5))
         .Text(d.Mm(5), d.Mm(18), "QTY : {Qty}", d.Mm(3.5))
         .Barcode(d.Mm(5), d.Mm(24), BarcodeType.Code128, "{PartNo}", d.Mm(10), dpi >= 300 ? 3 : 2)
         .Qr(d.Mm(72), d.Mm(14), "{PartNo};{Qty};{Lot}", dpi >= 300 ? 6 : 4)
         .Text(d.Mm(5), d.Mm(40), "{NameJp}", d.Mm(4), style: TextStyle.Auto);
        return d.Bind(new Dictionary<string, string>
        {
            ["PartNo"] = "P2000800", ["Qty"] = "100", ["Lot"] = "L20261006", ["NameJp"] = "部品テスト",
        });
    }

    /// <summary>Nhãn mẫu có biến {..} để in hàng loạt từ CSV.</summary>
    public static LabelDocument Template(int dpi = 203)
    {
        var t = new LabelDocument { Dpi = dpi, JobName = "{PartNo}" };
        t.Width = t.Mm(100);
        t.Height = t.Mm(50);
        t.Gap = t.Mm(3);
        t.Box(t.Mm(2), t.Mm(2), t.Mm(96), t.Mm(46), 3)
         .Text(t.Mm(5), t.Mm(4), "{Title}", t.Mm(5))
         .Line(t.Mm(2), t.Mm(11), t.Mm(96))
         .Text(t.Mm(5), t.Mm(13), "PART: {PartNo}", t.Mm(3.5))
         .Text(t.Mm(5), t.Mm(18), "QTY : {Qty}", t.Mm(3.5))
         .Barcode(t.Mm(5), t.Mm(24), BarcodeType.Code128, "{PartNo}", t.Mm(10), dpi >= 300 ? 3 : 2)
         .Qr(t.Mm(72), t.Mm(14), "{PartNo};{Qty};{Lot}", dpi >= 300 ? 6 : 4)
         .Text(t.Mm(5), t.Mm(40), "{Name}", t.Mm(4));
        return t;
    }

    public static string ToJson(LabelDocument doc) => JsonSerializer.Serialize(doc, ProfileStore.JsonOptions);

    public static LabelDocument FromJson(string json) =>
        JsonSerializer.Deserialize<LabelDocument>(json, ProfileStore.JsonOptions)
        ?? throw new InvalidDataException("JSON nhãn không hợp lệ.");
}
