# PrinterHub

Phần mềm Windows (.NET 10) gửi lệnh tự động đến nhiều dòng máy in: **SATO, Zebra (ZDesigner), Godex, FUJIFILM, Fujitsu**…
Xem kế hoạch tổng thể trong [PLAN.md](PLAN.md).

## Cấu trúc

```
PrinterHub.sln
├─ src/
│  ├─ PrinterHub.Core         Model nhãn trung lập, PrinterStatus, RouteConfig/Profile, PrinterService (retry + failover),
│  │                          ControlChars (<STX><ESC>…), CP932, CSV, Code128
│  ├─ PrinterHub.Languages    SBPL (SATO) · ZPL/SZPL/GZPL (Zebra) · EZPL (Godex) · PJL (FUJIFILM/văn phòng) · RAW
│  │                          + bộ phân tích trạng thái: SATO Status4 (ENQ), Zebra ~HS, Godex ~S,CHECK, PJL INFO STATUS
│  ├─ PrinterHub.Transports   tcp:// (9100) · sato4:// (Status4 2 cổng) · lpr:// (RFC 1179) · file:// (UNC/LPT) · quét mạng
│  ├─ PrinterHub.Windows      spooler:// (WritePrinter RAW) · serial:// (COM/Bluetooth SPP, Win32) · liệt kê máy in/job
│  ├─ PrinterHub.Cli          printerhub.exe – dòng lệnh, in hàng loạt CSV, hot folder
│  └─ PrinterHub.App          GUI WinForms (dark theme), in GDI qua driver, passthrough ${…}$
├─ tools/PrinterHub.Simulator Máy in giả lập: 9100 + SATO 1024/1025 + LPD 5515
├─ tests/PrinterHub.Tests     37 test (không cần NuGet)
└─ samples/                   profiles.json · label.json · data.csv
```

## Build & chạy

Yêu cầu: **.NET 10 SDK** (Visual Studio 2026 hoặc `dotnet` CLI) trên Windows 10/11.

```powershell
dotnet build PrinterHub.sln
dotnet run --project tests/PrinterHub.Tests          # chạy test
dotnet run --project tools/PrinterHub.Simulator      # máy in giả lập (cửa sổ riêng)
dotnet run --project src/PrinterHub.App              # GUI
```

## CLI nhanh

```powershell

# Nhãn mẫu (khung, chữ, Code128, QR, Kanji) bằng SBPL qua SATO Status4
printerhub label --route "sato4://192.168.1.50?data=1024&status=1025" --lang SBPL --copies 3

# Cùng nhãn cho Zebra / Godex
printerhub label --route tcp://192.168.1.51:9100 --lang ZPL --opt unicodeFont=E:ANMDJ.TTF
printerhub label --route tcp://192.168.1.52:9100 --lang EZPL

# Qua driver Windows (USB/LPT/COM...) dạng RAW
printerhub label --route "spooler://SATO CL4NX Plus 305dpi" --lang SBPL

# Lệnh raw với ký hiệu điều khiển, chờ phản hồi
printerhub raw --route tcp://192.168.1.50:9100 --text "<ENQ>" --wait 1000
printerhub raw --route tcp://192.168.1.50:9100 --sbpl --encoding shift_jis --text "<STX><A><V>100<H>100<K9B>部品<Q>1<Z><ETX>"

# Trạng thái (lặp mỗi giây)
printerhub status --route "sato4://192.168.1.50" --lang SBPL --repeat 1000

# In hàng loạt từ CSV (UTF-8 hoặc Shift-JIS tự nhận), hoặc xuất file lệnh để kiểm tra
printerhub batch --json samples/label.json --csv samples/data.csv --route tcp://192.168.1.50:9100 --lang SBPL
printerhub batch --json samples/label.json --csv samples/data.csv --lang ZPL --out out/

# In theo profile (tự failover sang route dự phòng) và hot folder
printerhub print --profile SATO-CL4NX --profiles samples/profiles.json
printerhub watch --folder C:\PrintIn --profile SATO-CL4NX --template samples/label.json

# Gửi PDF tới máy FUJIFILM qua 9100 có header PJL
printerhub send --route tcp://192.168.1.60:9100 --lang PJL --format PDF --file report.pdf

printerhub discover --subnet 192.168.1          # quét 9100/1024/1025/515/631/6101/9200
printerhub printers                              # máy in Windows
printerhub help
```

## Route

| Route | Ý nghĩa | Hai chiều |
|---|---|---|
| `tcp://host:9100` | Raw TCP (SATO/Zebra/Godex/FUJIFILM) | ✅ |
| `sato4://host?data=1024&status=1025` | SATO Status4 hai cổng | ✅ (cổng status) |
| `lpr://host[:515]/queue` | LPR/LPD | ❌ |
| `spooler://Tên máy in` | Windows Spooler RAW (mọi driver) | ❌ (`?bidi=true` thử ReadPrinter) |
| `passthrough://Tên máy in` | Driver passthrough `${…}$` (chỉ GUI) | ❌ |
| `serial://COM3?baud=9600&handshake=rtscts` | RS-232C / USB-Serial / Bluetooth SPP | ✅ |
| `file://\\PC\Share` | Máy in chia sẻ, `\\.\LPT1`, hoặc file | ❌ |

Tuỳ chọn chung: `connectTimeout=3000`, `readTimeout=2000`.

## Tiến độ so với PLAN.md

| Giai đoạn | Trạng thái |
|---|---|
| P0 Nền tảng (Core, Simulator, CLI) | ✅ |
| P1 Raw transports (TCP, Spooler RAW, UNC, Serial, LPR) | ✅ (Spooler/Serial cần test trên Windows) |
| P2 SATO (SBPL, Kanji CP932, Status3/4/5 parser, 2 cổng, template-ready) | ✅ phần lõi — cần xác minh lệnh trên máy thật |
| P3 Zebra & Godex (ZPL ^CI28, ~HS; EZPL, ~S,CHECK) | ✅ phần lõi — chưa tích hợp Zebra SDK / EZio DLL |
| P4 Driver/Windows (GDI PrintDocument, passthrough, liệt kê job) | ✅ trong GUI — chưa có XPS/System.Printing, USB trực tiếp, Bluetooth WinRT |
| P5 Office (PJL wrap/status) | ✅ một phần — chưa có IPP, SNMP |
| P6 GUI WinForms | ✅ bản đầu |
| P7 Tự động hoá (hot folder, batch CSV, failover) | ✅ CLI — chưa có Windows Service/REST |

## ⚠ Cần xác minh trên máy in thật

Các lệnh dưới đây viết theo tài liệu hãng nhưng **chưa thử trên thiết bị**:

- SBPL: Kanji `ESC K9B` (+ `ESC KC` chọn mã), QR `ESC 2D30 … ESC DN`, Code128 `ESC BG … >H`, ảnh `ESC GH`, bảng mã trạng thái Status4 (chữ thường = lỗi).
- EZPL: QR `W…`, ảnh `GW…` (có đảo bit `imageInvert`), văn bản Unicode `AT,…`.
- ZPL: văn bản Kanji/Tiếng Việt cần font TTF trong máy in (`--opt unicodeFont=E:xxx.TTF`).
- GUI `PrinterHub.App` được viết cho Windows và chưa build trong môi trường phát triển hiện tại (Linux, không có WindowsDesktop SDK) — build lần đầu trên Windows có thể cần sửa nhỏ.
