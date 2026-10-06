# Kế hoạch: PrinterHub — phần mềm gửi lệnh in đa hãng (Windows 10, .NET 10)

> Phiên bản: 0.1 · Ngày: 06/10/2026 · Tác giả: Tan Vu (FSTV)
> Thư mục: `C:\Github\samples.cs\printer` · Tham khảo phong cách: `C:\Github\samples.cs\cp932` (WinForms code-only, dark theme, đăng ký CodePages CP932)

---

## 1. Mục tiêu

Một ứng dụng Windows 10 chạy trên **.NET 10 (LTS)** có thể gửi lệnh/tài liệu in **tự động** đến nhiều hãng máy in — **SATO, Godex, Zebra (ZDesigner), FUJIFILM (Fuji Xerox), Fujitsu**, và mở rộng cho hãng khác — bằng **mọi phương thức mà máy in hỗ trợ**, có đọc trạng thái hai chiều khi máy in cho phép.

Tiêu chí thành công:

1. Cùng một "job" (dữ liệu nhãn + template) có thể in qua ≥ 3 đường khác nhau cho mỗi hãng.
2. Đọc được trạng thái (online / hết giấy / mở đầu in / hết ribbon / đang in / số nhãn còn lại) cho SATO, Zebra, Godex, FUJIFILM.
3. In được tiếng Nhật (Shift-JIS/CP932, UTF-8) và tiếng Việt (UTF-8 + font TTF).
4. Có chế độ tự động: thư mục theo dõi (hot folder), CLI, hàng đợi có retry.

---

## 2. Quyết định nền tảng

| Hạng mục | Lựa chọn | Lý do |
|---|---|---|
| Runtime | `net10.0` (core) + `net10.0-windows10.0.19041.0` (app) | .NET 10 LTS; TFM Windows để dùng WinRT (Bluetooth, PDF render) |
| UI | **WinForms** (khuyến nghị) | WinUI 3 **không hỗ trợ PrintManager trên Windows 10** (chỉ Win 11). WinForms có sẵn PrintDocument/PrintPreview/PrintDialog, đồng bộ với dự án cp932 |
| UI phương án B | WinUI 3 cho giao diện + dùng GDI/WPF/RAW để in | Chỉ nếu bắt buộc giao diện Fluent; tốn công interop |
| P/Invoke | `Microsoft.Windows.CsWin32` | Sinh mã Spooler/SetupAPI an toàn, AOT-friendly |
| Encoding | `System.Text.Encoding.CodePages` (CP932) | Giống cp932: đăng ký provider ở `Program.Main` |
| Logging | `Microsoft.Extensions.Logging` + Serilog file | Lưu hex-dump byte gửi/nhận để debug |
| DI/cấu hình | `Microsoft.Extensions.Hosting` + `appsettings.json` | Dùng chung cho App, CLI, Service |

---

## 3. Bản đồ tất cả phương thức in

### 3.1 Ma trận phương thức × hãng

✅ hỗ trợ · ⚠️ tùy model/option · ❌ không · — không áp dụng

| # | Phương thức | SATO | Zebra | Godex | FUJIFILM BI | Fujitsu |
|---|---|---|---|---|---|---|
| T1 | Raw TCP (port 9100 / cấu hình) | ✅ 3 cổng cấu hình được (thường 1024/1025/9100) | ✅ 9100, 6101 (mobile) | ✅ 9100 *(xác minh)* | ✅ Port9100 | ⚠️ máy có Ethernet |
| T2 | TCP 2 cổng data + status (SATO Status4) | ✅ | ✅ 9100 + 9200 JSON | ❌ | — | — |
| T3 | LPR/LPD (515) | ✅ | ✅ | ⚠️ | ✅ | ⚠️ |
| T4 | IPP / IPPS (631) | ❌ chưa thấy | ✅ Link-OS 7.4+ (chỉ ZPL/raw) | ❌ | ✅ (Mopria) | ❌ |
| T5 | FTP put file | ✅ | ✅ | ⚠️ | ⚠️ | ❌ |
| T6 | Windows Spooler **RAW** (`WritePrinter`) | ✅ | ✅ | ✅ | ✅ (PCL/PS/PDF) | ✅ (ESC/P, ESC/POS) |
| T7 | Driver GDI (`PrintDocument`) | ✅ | ✅ | ✅ | ✅ | ✅ |
| T8 | Driver XPS / WPF `System.Printing` | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| T9 | Driver passthrough (lệnh nhúng trong văn bản GDI) | ✅ "Command Font" | ✅ `${ … }$` | ⚠️ (driver Seagull) | — | — |
| T10 | Copy tới máy in chia sẻ (`\\host\share`) | ✅ | ✅ | ✅ | ✅ | ✅ |
| T11 | Serial RS-232C / COM ảo | ✅ | ✅ | ✅ | ❌ | ✅ |
| T12 | USB trực tiếp (usbprint.sys / WinUSB) | ✅ | ✅ | ✅ (EZio) | ⚠️ | ✅ |
| T13 | Bluetooth SPP | ✅ (option) | ✅ (+BLE) | ✅ (MX, option) | ❌ | ✅ (FP-510II) |
| T14 | Parallel IEEE1284 | ⚠️ (option) | ⚠️ cũ | ⚠️ cũ | ❌ | ✅ DL series |
| T15 | SDK của hãng | ✅ Multi LABELIST Component (.NET 8) / SATOPrinterAPI (hết hỗ trợ 01/12/2026) | ✅ `Zebra.Printer.SDK` 5.x (`net10.0-windows`) | ✅ EZio DLL (P/Invoke) | ⚠️ Apeos Connect SDK (SOAP, phía MFP) | ⚠️ OPOS/JavaPOS |
| T16 | PDF Direct (gửi PDF thô, máy in tự render) | ⚠️ firmware PDF Direct Print | ⚠️ PDF Direct (Link-OS 6.3+) | ❌ | ✅ | ❌ |
| T17 | Template lưu trong máy in + chỉ gửi dữ liệu biến | ✅ `ESC YS/YR`, `&S/&R` | ✅ `^DF/^XF` | ✅ (form/graphic memory) | — | — |
| T18 | XML-enabled printing | ✅ (e-series/XML template) | ✅ | ❌ | — | — |
| T19 | Ứng dụng chạy trên máy in (không cần PC) | ✅ AEP / Web AEP | ✅ Link-OS apps | ❌ | ⚠️ XCP plugin | ❌ |
| T20 | Máy in kết nối ra server (cloud) | ⚠️ SOS (chỉ quản lý, không in) | ✅ Weblink WebSocket | ❌ | ⚠️ Apeos iiX | ❌ |
| T21 | Email print | ❌ | ❌ | ❌ | ⚠️ | ❌ |
| S1 | Trạng thái SNMP (RFC 3805 + MIB riêng) | ✅ | ✅ | ⚠️ | ✅ | ⚠️ |
| S2 | Trạng thái qua WMI (`Win32_Printer`) | ✅ (qua driver) | ✅ | ✅ | ✅ | ✅ |

### 3.2 Ngôn ngữ lệnh

| Hãng | Native | Emulation / khác |
|---|---|---|
| **SATO** | **SBPL** (`STX ESC A … ESC Z ETX`) | SZPL (Zebra), SDPL (Datamax), SIPL (Intermec), STCL (Toshiba TEC), SEPL (Eltron), SCPL/SCPCL, SPOS (dòng PW); PDF (firmware PDF Direct). S84NX/S86NX chủ yếu dùng SZPL/SDPL/SIPL |
| **Zebra** | **ZPL II** | EPL2 (ZD4xx/6xx), CPCL (ZQ), SGD (`! U1 setvar/getvar/do`), JSON SGD (`{}{…}`), XML, PDF Direct |
| **Godex** | **EZPL** | GEPL (EPL), GZPL (ZPL), GDPL (DPL) — tùy model |
| **FUJIFILM BI** | ART EX (JP), PCL5/PCL6 (Global) | PostScript 3 (option), PDF/TIFF/JPEG/XPS/DocuWorks direct, ESC/P (VP-1000), HP-GL/2, PJL *(cần test)* |
| **Fujitsu** | DPL24C PLUS / FM sequence (DL dot-matrix) | ESC/P2, IBM Proprinter; ESC/POS (receipt FP-series) |

---

## 4. Chi tiết theo hãng

### 4.1 SATO (ưu tiên cao nhất)

**Model mục tiêu:** CL4NX Plus / CL6NX Plus, CT4-LX, FX3-LX, S84NX/S86NX, WS2/WS4, CG2/CG4, PW2NX/PW4NX, M-84Pro (cũ).

**Ngôn ngữ SBPL — phần cần implement trong `SbplBuilder`:**

- Khung job: `STX ESC A … ESC Z ETX`; tùy chọn ký tự thay thế (STX=`{`, ETX=`}`, ESC=`^`) khi host không gửi được control byte.
- Vị trí/ phóng to: `ESC H`, `ESC V`, `ESC L`, `ESC P`; số lượng `ESC Q`; Job ID `ESC ID`; tên job `ESC WK`.
- Mã vạch: `ESC B/D/BG` (Code128…), `ESC 2D` (QR, DataMatrix, PDF417, Aztec).
- **Kanji:** `ESC KC` chọn mã (JIS / Shift-JIS / UTF-8 / UTF-16…), `ESC K1…KD` bitmap kanji, `ESC $=` outline, `ESC RD/RH` font CG/scalable (UTF-8). → chuỗi tiếng Nhật encode bằng CP932, tiếng Việt dùng UTF-8 + font scalable/TTF đã tải.
- Template: `ESC YS/YR` (format), `ESC &S/&R` (form overlay), `ESC /N /D` (field), `ESC GI/GR` (graphic), `ESC /Y…` (XML template).
- Lệnh hệ thống DC2: `PA` cài đặt, `PB/PC` thông tin, `PG` trạng thái, `DB` khởi tạo, `DC` reset.
- Ảnh: chuyển bitmap → `ESC GH/GB` (hex/binary) bằng bộ chuyển đổi riêng.

**Giao thức truyền & trạng thái:**

| Chế độ | Cổng | Ghi chú |
|---|---|---|
| Status4 (2 cổng) | Port1 = data (1024), Port2 = status (1025) | Máy in tự gửi trạng thái chu kỳ 100–999 ms hoặc khi nhận `ENQ` |
| Status4 ENQ | 1 cổng | Host gửi `ENQ (0x05)` → nhận `STX` + ID(2) + status(1) + còn lại(6) + tên job(16) + `ETX` (LAN có thêm header) |
| Status3 | 1 cổng | Giao thức cũ |
| Status5 | 1 cổng | Thêm kiểm tra BCC + số item |
| Port3 (thường 9100) | 1 cổng | Data + status, có "Legacy status" |
| Driver | 9100 | Kênh 2 chiều của driver SATO |

Mã trạng thái (cần đối chiếu Programming Reference): nhóm ký tự `0–3` offline, `A–D` chờ dữ liệu, `G–J` đang in, `M–P` chờ bóc nhãn, `S–V` đang phân tích; chữ thường = lỗi (`b` mở đầu in, `c` hết giấy, `d` hết ribbon…). `CAN` để hủy job.

**Các đường khác của SATO:**

- **Driver SATO** (của SATO hoặc Seagull): GDI/XPS, RAW spooler, **Command Font** (passthrough SBPL), 2 chiều nếu bật bidi.
- **Multi LABELIST Component** (.NET 8, phát hành 01/2026): kết nối USB/LAN/COM/Bluetooth không cần driver, có trạng thái → nạp từ .NET 10 qua adapter. `SATOPrinterAPI.dll` (.NET Framework) **hết hỗ trợ 01/12/2026** → không dùng cho code mới.
- **LPD 515, FTP**, **PDF Direct Print** (firmware riêng), **AEP / Web AEP** (app chạy trên máy in), **SOS** (giám sát cloud, không in), **All-In-One Tool** (cấu hình, tải font), **WebConfig**, **SNMP**.
- **Ngắt kết nối ngoài (EXT/PLC)**: tín hiệu I/O cho dây chuyền — ghi nhận cho giai đoạn sau.

### 4.2 Zebra (ZDesigner)

- **ZPL II** + `^CI28` (UTF-8) + font scalable/TTF (`^A@`, `^CW`); template `^DF`/`^XF`; trạng thái `~HS`, `~HQES`, SGD `device.host_status`.
- **SGD/JSON SGD** trên cổng 9200 (đọc trạng thái khi 9100 đang bận).
- **Link-OS SDK** `Zebra.Printer.SDK` **5.x** target `net10.0-windows10.0.26100` → khớp .NET 10 (cần VS 2026; kiểm tra chạy được trên Win10 khi đặt `SupportedOSPlatformVersion`). Dùng: `TcpConnection`, `MultichannelTcpConnection`, `DriverPrinterConnection`, `UsbConnection`, discovery, `PrintStoredFormat`.
- **ZDesigner passthrough** `${ … }$` (cấu hình trong Printing Preferences, không có API).
- **IPP** (Link-OS 7.4+, chỉ raw ZPL), **PDF Direct**, **Weblink** (máy in mở WebSocket tới server ASP.NET Core), **Browser Print** (dịch vụ localhost cho web).

### 4.3 Godex

- **EZPL** (lệnh kết thúc CR, `^Q ^W ^L … E`), emulation GZPL/GEPL/GDPL tùy model.
- Trạng thái: `^XSET,IMMEDIATE,1` rồi `~S,CHECK` → `00` Ready, `01/02` hết giấy/kẹt, `03` hết ribbon, `04` mở đầu in, `20` pause, `50` đang in…
- Unicode/TTF: `~H,TTF`, `~H,TTF_TABLE`; xóa bộ nhớ `~MDEL`.
- **EZio SDK** (`Ezio32.dll`/`Ezio64.dll`, stdcall): `openport`, `setup`, `sendcommand`, `ecTextOut` (render font Windows thành ảnh — in tiếng Việt/Nhật không cần tải font), `closeport`. Dùng `[LibraryImport]`, khớp bitness x64.
- Raw TCP 9100 (xác minh bằng NetSetting), RAW spooler, COM, Bluetooth (MX).

### 4.4 FUJIFILM Business Innovation (ApeosPort/Apeos/DocuPrint) — máy in văn phòng

- In tài liệu A4/A3 chứ không phải nhãn: PDF, PCL6, PostScript, XPS.
- Đường ưu tiên: **IPP/IPPS** (SharpIppNext, Mopria) → **Port9100/LPD gửi PDF thô** (PDF direct) → **driver GDI/XPS** → **RAW PCL/PS + PJL header** (copies, duplex, job name).
- Model Nhật dùng **ART EX** mặc định, PCL có thể là option → phải dò khả năng (IPP `document-format-supported`).
- Trạng thái: SNMP Printer-MIB (`hrPrinterStatus`, `prtMarkerSuppliesLevel`, `prtAlertTable`) hoặc IPP `printer-state-reasons`.

### 4.5 Fujitsu

- Dot-matrix DL series: ESC/P2 / DPL24C / IBM Proprinter qua RAW spooler, COM, LPT.
- Receipt FP-series: ESC/POS qua COM/USB/Ethernet/Bluetooth, hoặc OPOS.

### 4.6 Hãng khác (mở rộng sau)

Toshiba TEC (TPCL), Honeywell/Intermec (DP/IPL/Fingerprint), Datamax (DPL), Epson (ESC/POS, ESC/Label), TSC (TSPL), Brother (P-touch Template, ESC/P), Citizen — chỉ cần thêm `ICommandLanguage` + profile.

---

## 5. Kiến trúc phần mềm

```
printer/
├─ PrinterHub.sln
├─ src/
│  ├─ PrinterHub.Core/            (net10.0)  Abstractions, model, pipeline, queue
│  ├─ PrinterHub.Languages/       (net10.0)  SBPL, ZPL, EZPL, EPL, DPL, PCL/PJL, ESC/P, ESC/POS builders + image→command
│  ├─ PrinterHub.Transports/      (net10.0)  TCP raw, SATO Status4, LPR, FTP, IPP, Serial, UNC share
│  ├─ PrinterHub.Windows/         (net10.0-windows10.0.19041.0)  Spooler RAW, GDI, XPS, passthrough, USB print, Bluetooth, WMI
│  ├─ PrinterHub.Status/          (net10.0)  SATO ENQ, Zebra ~HS/JSON, Godex ~S, SNMP, IPP attrs
│  ├─ PrinterHub.Vendors.Sato/    Adapter Multi LABELIST Component
│  ├─ PrinterHub.Vendors.Zebra/   Adapter Zebra.Printer.SDK 5.x
│  ├─ PrinterHub.Vendors.Godex/   P/Invoke EZio
│  ├─ PrinterHub.App/             (WinForms) GUI dark theme kiểu cp932
│  ├─ PrinterHub.Cli/             printerhub send / status / discover
│  └─ PrinterHub.Service/         (tùy chọn) Windows Service + hot folder + REST
├─ tools/
│  └─ PrinterHub.Simulator/       Giả lập máy in TCP (9100, SATO 1024/1025 trả ENQ, Zebra ~HS, Godex ~S)
├─ tests/
│  ├─ PrinterHub.Languages.Tests/ Golden-file test (so sánh byte)
│  └─ PrinterHub.Transports.Tests/ Test với Simulator
└─ docs/ PLAN.md, protocol notes, hex dumps mẫu
```

### 5.1 Interface chính

```csharp
public interface IPrinterTransport : IAsyncDisposable
{
    string Kind { get; }                        // "tcp-raw", "sato-status4", "spooler-raw", "ipp"...
    Task OpenAsync(CancellationToken ct);
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
    Task<int> ReceiveAsync(Memory<byte> buffer, TimeSpan timeout, CancellationToken ct); // 0 nếu 1 chiều
    bool IsBidirectional { get; }
}

public interface ICommandLanguage
{
    string Name { get; }                        // "SBPL", "ZPL", "EZPL", "PCL6"...
    Encoding TextEncoding { get; }              // CP932 / UTF-8
    byte[] Render(LabelDocument doc);           // model trung lập → byte
    byte[] BuildStatusRequest();
    PrinterStatus? ParseStatus(ReadOnlySpan<byte> reply);
}

public interface IPrintRoute                    // = Language + Transport (+ Status)
{
    Task<PrintResult> PrintAsync(PrintJob job, CancellationToken ct);
    Task<PrinterStatus> GetStatusAsync(CancellationToken ct);
}
```

- **`LabelDocument`** — mô hình nhãn trung lập: kích thước, DPI, các phần tử `Text`, `Barcode`, `QrCode`, `Image`, `Line`, `Box`, biến `{field}`.
- **`RawDocument`** — byte/tệp có sẵn (ZPL, SBPL, PRN, PDF) gửi nguyên trạng.
- **`PrinterProfile`** (JSON): hãng, model, DPI, ngôn ngữ, danh sách route ưu tiên, encoding, cổng, tùy chọn status.

```json
{
  "name": "SATO-CL4NX-Line1",
  "brand": "SATO", "model": "CL4NX Plus", "dpi": 305,
  "language": "SBPL", "encoding": "shift_jis",
  "routes": [
    { "kind": "sato-status4", "host": "192.168.1.50", "dataPort": 1024, "statusPort": 1025 },
    { "kind": "tcp-raw", "host": "192.168.1.50", "port": 9100 },
    { "kind": "spooler-raw", "queue": "SATO CL4NX Plus 305dpi" }
  ]
}
```

### 5.2 Pipeline gửi job

1. Nhận job (GUI / CLI / hot folder / REST) → thêm vào hàng đợi (Channel<T>).
2. Chọn route theo profile; **kiểm tra trạng thái trước** nếu route có hai chiều.
3. Render byte bằng `ICommandLanguage` (hoặc lấy raw).
4. Gửi; với SATO Status4 theo dõi số nhãn còn lại đến 0; Zebra poll `~HS` / JSON.
5. Lỗi → retry có backoff → nếu vẫn lỗi chuyển sang **route dự phòng**.
6. Ghi log + hex-dump; cập nhật UI.

---

## 6. Giao diện (WinForms, dark theme giống cp932)

- **Tab Máy in:** danh sách profile, nút **Discover** (quét subnet port 9100/1024/515/631, mDNS `_pdl-datastream._tcp`/`_ipp._tcp`, SNMP sysDescr, danh sách Windows queue + WMI).
- **Tab Gửi lệnh:** chọn máy in + route + ngôn ngữ; editor raw command (hiển thị ký tự điều khiển dạng `<STX>`, `<ESC>`), chọn encoding (UTF-8 / CP932), Send, xem phản hồi hex/ASCII.
- **Tab Nhãn:** thiết kế nhãn đơn giản (text/barcode/QR/ảnh), preview, nhập CSV (tái dùng `CsvUtility` từ cp932 cho file Shift-JIS) để in hàng loạt.
- **Tab Trạng thái:** đèn trạng thái từng máy, số nhãn còn lại, log realtime.
- **Tab Thư viện lệnh:** snippet có sẵn cho mỗi hãng (in test, calibrate, reset, đọc cấu hình).

---

## 7. Lộ trình triển khai

| Giai đoạn | Nội dung | Kết quả |
|---|---|---|
| **P0 — Nền tảng** (1 tuần) | Solution, Core interfaces, logging, cấu hình profile, **Simulator TCP** | In "hello" tới simulator qua CLI |
| **P1 — Raw transports** (1–2 tuần) | T1 TCP raw, T6 Spooler RAW (CsWin32), T10 UNC, T11 Serial, T3 LPR | Gửi file `.prn` bất kỳ tới mọi hãng |
| **P2 — SATO** (2 tuần) | `SbplBuilder` đầy đủ + Kanji CP932, Status4 2 cổng & ENQ, Status3/5, mã trạng thái, template `YS/YR`, ảnh → SBPL, driver Command Font, FTP/LPD | In nhãn SATO tiếng Nhật, theo dõi số nhãn còn lại |
| **P3 — Zebra & Godex** (2 tuần) | `ZplBuilder` (^CI28), `~HS`/JSON 9200, `^DF/^XF`; `EzplBuilder`, `~S,CHECK`; adapter Zebra SDK & EZio | Cùng một `LabelDocument` in ra 3 hãng |
| **P4 — Driver/Windows** (1–2 tuần) | T7 GDI `PrintDocument`, T8 XPS/`System.Printing`, T9 passthrough, WMI job monitor, USB print (usbprint.sys), Bluetooth SPP | In qua driver, xem trạng thái queue |
| **P5 — Office (FUJIFILM/Fujitsu)** (1 tuần) | IPP (SharpIppNext), PDF thô qua 9100/LPD, PCL/PJL header, SNMP Printer-MIB, ESC/P / ESC/POS | In PDF lên Apeos, đọc mức mực |
| **P6 — GUI** (2 tuần) | Các tab ở mục 6, discovery | Bản dùng thử nội bộ |
| **P7 — Tự động hóa** (1 tuần) | Hot folder, CLI hoàn chỉnh, (tùy chọn) Windows Service + REST, failover route | Chạy không người giám sát |
| **P8 — Nâng cao** (tùy chọn) | PDF Direct (SATO/Zebra), Zebra Weblink server, SATO AEP/Web AEP mẫu, Multi LABELIST, EXT/PLC | Báo cáo khả thi |

---

## 8. Kiểm thử

- **Golden-file test:** mỗi builder render `LabelDocument` mẫu → so sánh byte với file chuẩn (bắt lỗi encoding Kanji).
- **Simulator** trong CI: kiểm tra framing STX/ETX, phản hồi ENQ, timeout, mất kết nối giữa job.
- **Thiết bị thật:** checklist cho từng model × route × (ASCII, Kanji, tiếng Việt, QR, ảnh) × trạng thái lỗi (mở nắp, hết giấy, hết ribbon).
- **Ma trận Windows:** Win10 22H2 (mục tiêu), Win11 24H2 có/không bật **Windows Protected Print Mode**.

---

## 9. Rủi ro & lưu ý

| Rủi ro | Ảnh hưởng | Giảm thiểu |
|---|---|---|
| WinUI 3 không in được trên Win10 | Không dùng PrintManager | Chọn WinForms |
| Microsoft dừng phát hành driver bên thứ ba qua Windows Update (2026–2027), WPP trên Win11 chặn driver hãng | Đường driver yếu dần | Ưu tiên route trực tiếp (TCP/LPR/IPP/USB); driver chỉ là dự phòng |
| `SATOPrinterAPI` hết hỗ trợ 01/12/2026 | — | Dùng raw SBPL tự viết; Multi LABELIST là tùy chọn |
| Zebra SDK 5.x target `windows10.0.26100` | Có thể cần cấu hình để chạy Win10 | Kiểm tra sớm ở P3; fallback raw ZPL |
| Driver v4 / IPP class driver từ chối datatype RAW | Spooler RAW lỗi | Tạo queue "Generic / Text Only" hoặc dùng TCP |
| Mã trạng thái SATO, cổng mặc định Godex chưa xác minh 100% | Parse sai | Đối chiếu Programming Reference, test thiết bị thật |
| FUJIFILM bản Nhật mặc định ART EX, không có PCL | In PCL hỏng | Dò `document-format-supported` qua IPP, ưu tiên PDF |
| Encoding hỗn hợp CP932 / UTF-8 | Lỗi chữ | Encoding cấu hình theo profile + golden test |
| EZio DLL x86/x64 | Crash khi load | Build x64, đóng gói đúng DLL |

---

## 10. Câu hỏi cần chốt

1. Danh sách model **thực tế** đang có tại nhà máy (đặc biệt SATO: CL4NX Plus? CT4-LX? S84NX?) và firmware?
2. "Fuji" là FUJIFILM BI (máy văn phòng) hay Fujitsu? (kế hoạch đang bao cả hai)
3. Nguồn dữ liệu tự động: CSV/hot folder, database, ERP/MES, hay REST?
4. Có cần chạy như Windows Service không người dùng?
5. Có máy in thật để test sớm ở P2 không?

---

## 11. Tài liệu tham khảo

- SATO CL4NX Plus Programming Reference — https://satosudamerica.com/site/wp-content/uploads/2020/04/CL4NX_Plus_ProgrammingReference_ENG_01.pdf
- SATO emulation theo model — https://helpcenter.sato-global.com/hc/en-001/articles/43598470009881
- SATO LAN port / protocol — https://www.manual.sato-global.com/printer/clnxplus/main/main_GUID-92668984-8FA0-4A17-840B-DF448CE7AC2E.html
- SATO PDF Direct Print — https://www.manual.sato-global.com/software/PDFDirectPrint/main/c_Overview.html
- SATO Printer API EOL — https://helpcenter.sato-global.com/hc/en-001/articles/53951488744217
- Multi LABELIST Component — https://satoasiapacific.com/product/multi-labelist-component/
- SATO AEP — https://www.satoeurope.com/products/aep.php
- SATO Windows Driver manual — https://www.sato-global.com/files/Printer_Drivers/Windows_Printer_Driver/Printer_Driver_Installation_Manual_EN.pdf
- Zebra ZPL/SGD — https://docs.zebra.com/us/en/printers/software/zpl-pg/
- Zebra.Printer.SDK — https://www.nuget.org/packages/Zebra.Printer.SDK/
- Zebra IPP — https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/ipp-ipps-link-os-cg-en.pdf
- Godex EZPL manual — https://www.manualsdir.com/manuals/736893/godex-ezpl.html
- Godex download (EZio SDK) — https://godexintl.com.vn/en/download/
- FUJIFILM ApeosPort spec — https://assets-fb-rn.fujifilm.com/files/2025-11/1f190809999f223bc63ca109c6728a3e/DGE1506S_ApeosPort_C7070R_C6570R_C5570R_C4570R_C3570R_C2570R_spec.pdf
- Windows App SDK – tính năng hỗ trợ (PrintManager Win11) — https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/what-is-supported
- Kế hoạch ngừng driver bên thứ ba — https://learn.microsoft.com/en-us/windows-hardware/drivers/print/end-of-servicing-plan-for-third-party-printer-drivers-on-windows
- SharpIppNext — https://github.com/danielklecha/SharpIppNext
