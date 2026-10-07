# In với máy Zebra ZD421 bằng PrinterHub

> Tài liệu hướng dẫn dùng **PrinterHub** (GUI `PrinterHub.exe` và CLI `printerhub`) để in tới máy **Zebra ZD421** thật (ZD421d in nhiệt trực tiếp / ZD421t truyền nhiệt / ZD421c hộp ribbon).
> Các mục "⚠ cần xác minh" chưa được thử trên máy thật.

---

## 1. Thông tin máy ZD421 cần biết

| Hạng mục | Giá trị |
|---|---|
| Độ phân giải | **203 dpi** (1 mm ≈ 8 dot) hoặc **300 dpi** (1 mm ≈ 11.8 dot) — xem tem máy |
| Khổ in tối đa | 104 mm (4") |
| Cổng chuẩn | USB 2.0, USB Host |
| Cổng tuỳ chọn | Ethernet hoặc Serial (module lắp thêm), Wi-Fi + Bluetooth (bản Wireless) |
| Ngôn ngữ | **ZPL II**, EPL2, XML, Link-OS (SGD) |
| Cổng TCP | **9100** (raw), 6101 (raw/status phụ), 9200 (JSON SGD) ⚠ cần xác minh theo firmware |
| Driver Windows | **ZDesigner ZD421-203dpi ZPL** (hoặc 300dpi) |
| Lệnh trạng thái | `~HS` |

**Ngôn ngữ chọn trong PrinterHub:** `ZPL` · **Encoding:** `utf-8` (PrinterHub tự chèn `^CI28`).

---

## 2. Chuẩn bị

### 2.1 Chạy phần mềm

```powershell
cd C:\Github\samples.cs\printer
dotnet build PrinterHub.sln
dotnet run --project src\PrinterHub.App          # GUI
dotnet run --project src\PrinterHub.Cli -- help  # CLI (viết tắt: printerhub)
```

### 2.2 Lấy IP máy in

- Giữ nút **FEED + CANCEL** ~2 giây để in nhãn cấu hình ⚠ cần xác minh thao tác theo user guide; hoặc gửi `~WC` qua USB.
- Hoặc quét bằng PrinterHub:
  - **GUI:** tab **Tìm máy in mạng** → **▶ Quét** → dòng có cổng `9100`/`9200` được nhận diện "Zebra Link-OS" → **Dùng route đã chọn** (tự chọn ngôn ngữ ZPL).
  - **CLI:** `printerhub discover --subnet 192.168.1 --ports 9100,6101,9200,515,631`

### 2.3 Profile mẫu

```json
{
  "name": "ZEBRA-ZD421",
  "brand": "Zebra",
  "model": "ZD421",
  "dpi": 203,
  "language": "ZPL",
  "encoding": "utf-8",
  "routes": [
    "tcp://192.168.1.51:9100",
    "spooler://ZDesigner ZD421-203dpi ZPL",
    "file://\\\\PC01\\ZD421"
  ],
  "retryCount": 2,
  "retryDelayMs": 1000,
  "languageOptions": { "unicodeFont": "E:ARIALUNI.TTF" }
}
```

- Máy **300 dpi**: đổi `"dpi": 300` và đặt `"dpi": 300` trong JSON nhãn (toạ độ tính bằng dot).
- `unicodeFont`: font TTF đã có trong máy in, dùng cho chữ tiếng Việt/Nhật (xem mục 7).

---

## 3. Tất cả các cách in bằng PrinterHub

### Cách 1 — Raw TCP cổng 9100 (khuyến nghị khi có LAN/Wi-Fi)

| | |
|---|---|
| Route | `tcp://<IP>:9100` |
| Ngôn ngữ | `ZPL` |
| Hai chiều | ✅ `~HS`, SGD `getvar` |
| Cần driver | ❌ |

**GUI**
1. Route `tcp://192.168.1.51:9100` · Ngôn ngữ `ZPL` · Encoding `utf-8`.
2. **Trạng thái** → `Ready`.
3. Tab **Nhãn** → **Nhãn mẫu** → kiểm tra ZPL ở khung "Lệnh sinh ra" → **▶ In qua route**.

**CLI**
```powershell
printerhub label  --route tcp://192.168.1.51:9100 --lang ZPL --copies 3
printerhub status --route tcp://192.168.1.51:9100 --lang ZPL
# Máy 300 dpi
printerhub label  --route tcp://192.168.1.51:9100 --lang ZPL --dpi 300
```

### Cách 2 — Cổng 6101 (cổng raw phụ)

Route `tcp://192.168.1.51:6101` — dùng khi cổng 9100 đang bị ứng dụng khác chiếm ⚠ cần xác minh cổng có bật trên ZD421 (`ip.port_alternate`).

### Cách 3 — Lệnh ZPL / SGD tự viết (tab Lệnh raw)

**GUI:** tab **Lệnh raw** → **Mẫu** chọn một trong:
- `Zebra · ZPL – nhãn test (UTF-8)`
- `Zebra · ~HS – host status`
- `Zebra · SGD – getvar ngôn ngữ` → `! U1 getvar "device.languages"`
- `Zebra · SGD – thông tin máy` → `! U1 getvar "device.product_name"`
- `Zebra · JSON SGD (port 9200)` → đổi Route thành `tcp://IP:9200`
- `Zebra · Calibrate ~JC`

Đặt **Chờ phản hồi (ms)** = 1000 với lệnh có trả lời → **▶ Gửi**. Với ZPL có thể để bật "Bỏ xuống dòng của editor".

**CLI**
```powershell
printerhub raw --route tcp://192.168.1.51:9100 --text "^XA^CI28^FO50,50^A0N,40,40^FDXin chao^FS^XZ"
printerhub raw --route tcp://192.168.1.51:9100 --text "! U1 getvar \"device.languages\"<CR><LF>" --wait 1000
printerhub raw --route tcp://192.168.1.51:9200 --text "{}{\"device.friendly_name\":null}" --wait 1000
printerhub send --route tcp://192.168.1.51:9100 --file C:\labels\label.zpl
```

### Cách 4 — Qua driver ZDesigner, gửi RAW (Spooler)

Dùng khi cắm **USB** hoặc đã cài máy in trong Windows. Lệnh ZPL đi nguyên vẹn qua driver.

| | |
|---|---|
| Route | `spooler://ZDesigner ZD421-203dpi ZPL` |
| Hai chiều | ❌ |
| Cần driver | ✅ ZDesigner |

**GUI:** tab **Máy in Windows** → **Làm mới** → chọn `ZDesigner ZD421…` (hãng hiện "Zebra") → **Dùng spooler:// (RAW)** → in ở tab Nhãn hoặc Lệnh raw → **Xem hàng đợi** để kiểm tra job.

**CLI**
```powershell
printerhub printers
printerhub label --route "spooler://ZDesigner ZD421-203dpi ZPL" --lang ZPL
printerhub jobs  --printer "ZDesigner ZD421-203dpi ZPL"
```

### Cách 5 — GDI qua driver ZDesigner (in như hình)

Driver chuyển trang GDI thành ảnh. In được mọi font Windows (tiếng Việt, Kanji) không cần font trong máy in; chậm hơn ZPL và mã vạch là ảnh.

**GUI:** chọn máy ở tab Máy in Windows (hoặc Route `spooler://ZDesigner…`) → tab **Nhãn** → **In qua driver (GDI)**.
Khổ giấy = `width × height` của nhãn (dot) ÷ dpi. CLI chưa hỗ trợ.

> Xem trước GDI vẽ thật Code128; QR/mã khác là khung giữ chỗ → dùng ZPL nếu cần QR.

### Cách 6 — Driver passthrough `${ … }$`

ZDesigner nhận ra đoạn văn bản nằm giữa `${` và `}$` và gửi nguyên ZPL xuống máy.

| | |
|---|---|
| Route | `passthrough://ZDesigner ZD421-203dpi ZPL` (mặc định `prefix=${`, `suffix=}$`) |
| Yêu cầu | Trong *Printing Preferences → Advanced Setup → Miscellaneous* bật passthrough và kiểm tra ký tự bắt đầu/kết thúc ⚠ tên mục có thể khác theo phiên bản driver |

**GUI:** tab Máy in Windows → chọn máy → **Dùng passthrough://** → tab Lệnh raw / Nhãn → gửi.
Dùng khi chỉ được in qua driver (máy chủ in, chính sách công ty) nhưng vẫn muốn lệnh ZPL gốc.

### Cách 7 — LPR/LPD

```powershell
printerhub label --route lpr://192.168.1.51/lp --lang ZPL
```
Một chiều. ⚠ cần xác minh LPD đang bật trên máy.

### Cách 8 — Cổng COM: Serial module hoặc Bluetooth

- **Serial (module):** `serial://COM3?baud=9600&handshake=rtscts` (cài đặt phải trùng máy in ⚠).
- **Bluetooth Classic:** ghép đôi trong Windows → Windows tạo **cổng COM ảo (Outgoing)** → `serial://COM7`.

```powershell
printerhub status --route "serial://COM7" --lang ZPL
printerhub label  --route "serial://COM7" --lang ZPL
```
Hai chiều ✅. Bluetooth LE chưa hỗ trợ.

### Cách 9 — Máy in chia sẻ Windows

ZD421 cắm USB vào `PC01`, chia sẻ tên `ZD421`:
```powershell
printerhub label --route "file://\\PC01\ZD421" --lang ZPL
```

### Cách 10 — In hàng loạt CSV

- **GUI:** tab Nhãn → **Template {biến}** / **Mở JSON...** → **In hàng loạt CSV...**
- **CLI:**
```powershell
printerhub batch --json samples\label.json --csv samples\data.csv --route tcp://192.168.1.51:9100 --lang ZPL
printerhub batch --json samples\label.json --csv samples\data.csv --lang ZPL --out out\zebra   # chỉ xuất .zpl
```
Có thể mở file `.zpl` xuất ra bằng trình xem ZPL online (vd labelary.com) để kiểm tra bố cục trước khi in.

### Cách 11 — In theo profile (failover)

- **GUI:** tab **Profiles** → `ZEBRA-ZD421` → **▶ In nhãn hiện tại (failover)**.
- **CLI:** `printerhub print --profile ZEBRA-ZD421 --profiles samples\profiles.json`

### Cách 12 — Hot folder

```powershell
printerhub watch --folder C:\PrintIn\Zebra --profile ZEBRA-ZD421 --profiles samples\profiles.json --template samples\label.json
```
Thả `*.zpl` / `*.prn` (raw), `*.json` (nhãn), `*.csv` (hàng loạt).

### Cách 13 — PDF Direct ⚠

ZD421 (Link-OS 6.3+) có thể in **PDF trực tiếp** sau khi cài "PDF Direct" và bật `! U1 setvar "apl.enable" "pdf"`. Khi đã bật:
```powershell
printerhub send --route tcp://192.168.1.51:9100 --file label.pdf
```
⚠ cần xác minh firmware và cách kích hoạt; khi bật PDF Direct máy không nhận ZPL — tắt lại bằng `! U1 setvar "apl.enable" "none"`.

---

## 4. Bảng tóm tắt

| # | Cách | Route | Hai chiều | Cần driver | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | Raw TCP 9100 | `tcp://IP:9100` | ✅ | ❌ | ⭐ chính |
| 2 | Cổng 6101 | `tcp://IP:6101` | ✅ | ❌ | ⚠ dự phòng |
| 3 | ZPL/SGD tự viết | bất kỳ | tuỳ route | tuỳ route | cấu hình, chẩn đoán |
| 4 | Spooler RAW | `spooler://ZDesigner…` | ❌ | ✅ | ⭐ khi cắm USB |
| 5 | GDI | (GUI) | ❌ | ✅ | font Windows, logo |
| 6 | Passthrough | `passthrough://ZDesigner…` | ❌ | ✅ | qua máy chủ in |
| 7 | LPR | `lpr://IP/lp` | ❌ | ❌ | ⚠ |
| 8 | COM / Bluetooth | `serial://COMx` | ✅ | ❌ | máy không có LAN |
| 9 | Chia sẻ Windows | `file://\\PC\Share` | ❌ | ✅ (máy chủ) | dùng chung |
| 10–12 | CSV / profile / hot folder | profile | — | — | tự động hoá |
| 13 | PDF Direct | `tcp://IP:9100` | ❌ | ❌ | ⚠ |

---

## 5. Trạng thái máy in

PrinterHub gửi `~HS` và đọc 3 chuỗi trả về:

| Thông tin | Nguồn trong `~HS` | Hiển thị |
|---|---|---|
| Hết giấy | String 1, cờ *paper out* | Error · PaperOut |
| Pause | String 1, cờ *pause* | Offline |
| Tràn bộ đệm | String 1, *buffer full* | Error |
| Quá nhiệt / thiếu nhiệt | String 1 | Error · Temperature |
| Mở đầu in | String 2, *head up* | Error · HeadOpen |
| Hết ribbon | String 2, *ribbon out* | Error · RibbonOut |
| Số nhãn còn lại | String 2, *labels remaining* | Printing · Remaining=n |
| Nhãn chờ lấy | String 2, *label waiting* | WaitingForTakeOut |

```powershell
printerhub status --route tcp://192.168.1.51:9100 --lang ZPL --repeat 1000
```
Khi in qua route hai chiều, PrinterHub **kiểm tra `~HS` trước khi in** và dừng nếu máy báo lỗi.

---

## 6. Lệnh ZPL/SGD hữu ích

| Lệnh | Tác dụng |
|---|---|
| `~WC` | In nhãn cấu hình |
| `~JC` | Hiệu chỉnh cảm biến giấy |
| `~HS` | Trạng thái |
| `^XA^JUS^XZ` | Lưu cấu hình |
| `! U1 getvar "ip.addr"` | Đọc IP |
| `! U1 setvar "ip.dhcp.enable" "off"` | Tắt DHCP ⚠ |
| `! U1 do "device.reset" ""` | Khởi động lại máy |
| `~SD20` | Đặt độ đậm (0–30) |

---

## 7. Tiếng Việt / tiếng Nhật

- Font `^A0` sẵn có **không có glyph CJK**, dấu tiếng Việt có thể thiếu ⚠.
- Cách 1: tải font TTF Unicode vào máy (Zebra Setup Utilities → *Download fonts*), rồi đặt `unicodeFont` trong profile hoặc:
  ```powershell
  printerhub label --route tcp://IP:9100 --lang ZPL --opt unicodeFont=E:ARIALUNI.TTF
  ```
  PrinterHub tự dùng `^A@N,h,w,E:ARIALUNI.TTF` cho dòng có ký tự ngoài ASCII.
- Cách 2: **GDI qua driver** (Cách 5) — dùng font Windows, không cần tải font.

---

## 8. Checklist thử với máy thật

1. [ ] `~WC` in nhãn cấu hình → ghi IP, dpi, firmware Link-OS.
2. [ ] `tcp://IP:9100` → **Trạng thái** = Ready.
3. [ ] In **Nhãn mẫu** ZPL (đúng dpi) → kiểm tra khung, chữ, Code128, QR.
4. [ ] Mở nắp → **Trạng thái** = HeadOpen → thử in, PrinterHub phải chặn.
5. [ ] SGD `getvar "device.languages"` trả về (vd `zpl`).
6. [ ] Cổng 9200 JSON trả lời.
7. [ ] `spooler://ZDesigner…` (USB) in được.
8. [ ] GDI có chữ tiếng Việt.
9. [ ] Passthrough `${…}$`.
10. [ ] Batch CSV + failover (rút LAN → tự sang spooler).

---

## 9. PrinterHub chưa hỗ trợ cho Zebra

- Zebra Link-OS SDK (`Zebra.Printer.SDK`) — USB hai chiều, Bluetooth LE, tìm máy tự động.
- IPP, Weblink (máy in tự kết nối lên server), Browser Print.
- Lưu template trong máy (`^DF`/`^XF`) — vẫn gửi được bằng tab Lệnh raw, nhưng chưa có giao diện riêng.
- SNMP.
