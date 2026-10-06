namespace PrinterHub.Core.Model;

/// <summary>
/// Ảnh đơn sắc 1 bit/pixel, mỗi hàng được pad lên bội số 8 bit.
/// Bit = 1 nghĩa là chấm ĐEN (in), MSB là pixel bên trái.
/// Dùng chung cho SBPL (ESC GH), ZPL (^GFA), EZPL (GW).
/// </summary>
public sealed class MonoImage
{
    public static readonly MonoImage Empty = new(0, 0);

    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>Dữ liệu (BytesPerRow * Height). Base64 khi serialize JSON.</summary>
    public byte[] Data { get; set; }

    public int BytesPerRow => (Width + 7) / 8;

    public MonoImage() : this(0, 0) { }

    public MonoImage(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new byte[((width + 7) / 8) * height];
    }

    public bool this[int x, int y]
    {
        get => (Data[y * BytesPerRow + (x >> 3)] & (0x80 >> (x & 7))) != 0;
        set
        {
            ref byte b = ref Data[y * BytesPerRow + (x >> 3)];
            if (value) b |= (byte)(0x80 >> (x & 7));
            else b &= (byte)~(0x80 >> (x & 7));
        }
    }

    /// <summary>Tạo ảnh từ buffer xám/ARGB bất kỳ qua hàm lấy độ sáng (0-255) và ngưỡng.</summary>
    public static MonoImage FromLuminance(int width, int height, Func<int, int, int> luminance, int threshold = 128)
    {
        var img = new MonoImage(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (luminance(x, y) < threshold) img[x, y] = true;
        return img;
    }

    /// <summary>Trả về bản sao có chiều cao làm tròn lên bội số của <paramref name="multiple"/> (SBPL yêu cầu bội số 8).</summary>
    public MonoImage PadHeight(int multiple)
    {
        int h = (Height + multiple - 1) / multiple * multiple;
        if (h == Height) return this;
        var img = new MonoImage(Width, h);
        Buffer.BlockCopy(Data, 0, img.Data, 0, Data.Length);
        return img;
    }

    public string ToHex() => Convert.ToHexString(Data);

    /// <summary>Ảnh kiểm tra dạng bàn cờ.</summary>
    public static MonoImage Checker(int width, int height, int cell = 8) =>
        FromLuminance(width, height, (x, y) => ((x / cell + y / cell) % 2 == 0) ? 0 : 255);
}
