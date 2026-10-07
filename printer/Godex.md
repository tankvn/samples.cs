# In với máy Godex G500 bằng PrinterHub

> Tài liệu hướng dẫn dùng **PrinterHub** (GUI `PrinterHub.exe` và CLI `printerhub`) để in tới máy **Godex G500** thật.
> Các mục ghi "⚠ cần xác minh" là thông tin chưa thử trên máy thật — hãy kiểm tra khi chạy lần đầu.

---

## 1. Thông tin máy Godex G500 cần biết

| Hạng mục | Giá trị |
|---|---|
| Loại | Máy in mã vạch để bàn, in nhiệt trực tiếp / truyền nhiệt |
| Độ phân giải | **203 dpi** → 1 mm ≈ 8 dot |
| Khổ in tối đa | 108 mm (4.25") |
| Cổng | USB (mọi bản); RS-232 + Ethernet tuỳ biến thể (G500U chỉ USB; G500 / G500UES có thêm Serial + LAN) ⚠ cần xác minh theo tem máy |
| Ngôn ngữ | **EZPL** (gốc), **GZPL** (giả lập Zebra ZPL), **GEPL** (giả lập EPL) — máy tự nhận dạng ⚠ cần xác minh |
| Cổng TCP raw | **9100** ⚠ cần xác minh bằng trang web cấu hình hoặc tool *Godex NetSetting* |
| Lệnh trạng thái | `~S,CHECK` (phải bật `^XSET,IMMEDIATE,1` trước) |

**Ngôn ngữ chọn trong PrinterHub:** `EZPL` (khuyến nghị) hoặc `GZPL` (nếu muốn dùng chung nhãn ZPL với Zebra).
**Encoding:** `utf-8`.

---

## 2. Chuẩn bị

### 2.1 Build / chạy phần mềm

```powershell
cd C:\Github\samples.cs\printer
dotnet build PrinterHub.sln
# GUI
dotnet run --project src\PrinterHub.App
# CLI (dưới đây viết tắt là: printerhub)
dotnet run --project src\PrinterHub.Cli -- help
#   hoặc: src\PrinterHub.Cli\bin\Debug\net10.0\printerhub.exe help
```

### 2.2 Lấy địa chỉ IP của máy (nếu có cổng LAN)

- In trang cấu hình: giữ nút **FEED** khi bật máy (self-test) ⚠ cần xác minh thao tác theo manual, hoặc gửi lệnh `~V` qua USB.
- Hoặc quét mạng bằng PrinterHub:
  - **GUI:** tab **Tìm máy in mạng** → nhập Subnet (vd `192.168.1`) → **▶ Quét** → chọn dòng có cổng `9100` → **Dùng route đã chọn**.
  - **CLI:** `printerhub discover --subnet 192.168.1 --ports 9100,515,80`

### 2.3 Khai báo profile (dùng cho in tự động / failover)

Tab **Profiles** (GUI) hoặc file `samples\profiles.json`:

```json
{
  "name": "GODEX-G500",
  "brand": "Godex",
  "model": "G500",
  "dpi": 203,
  "language": "EZPL",
  "encoding": "utf-8",
  "routes": [
    "tcp://192.168.1.52:9100?readTimeout=800",
    "spooler://Godex G500",
    "serial://COM3?baud=9600"
  ],
  "retryCount": 2,
  "retryDelayMs": 1000,
  "languageOptions": { "enableImmediateStatus": "true" }
}
```

- `enableImmediateStatus=true` → PrinterHub tự chèn `^XSET,IMMEDIATE,1` đầu mỗi nhãn để lệnh `~S,CHECK` hoạt động.
- Route được thử **theo thứ tự**: LAN lỗi → tự chuyển sang driver Windows → COM.

---

## 3. Tất cả các cách in bằng PrinterHub

### Cách 1 — Raw TCP cổng 9100 (khuyến nghị nếu máy có LAN)

| | |
|---|---|
| Route | `tcp://<IP>:9100` |
| Ngôn ngữ | `EZPL` (hoặc `GZPL`, `RAW`) |
| Hai chiều | ✅ đọc được trạng thái `~S,CHECK` |
| Cần driver | ❌ |

**GUI**
1. Ô **Route**: `tcp://192.168.1.52:9100` · **Ngôn ngữ**: `EZPL` · **Encoding**: `utf-8`.
2. Bấm **Trạng thái** → thanh trạng thái hiện `Ready` (màu xanh) nếu máy sẵn sàng.
3. Tab **Nhãn** → **Nhãn mẫu** → xem trước + lệnh EZPL sinh ra → **▶ In qua route**.

**CLI**
```powershell
printerhub label  --route tcp://192.168.1.52:9100 --lang EZPL --copies 2
printerhub status --route tcp://192.168.1.52:9100 --lang EZPL
```

### Cách 2 — Gửi lệnh EZPL tự viết (tab Lệnh raw)

Dùng khi muốn thử lệnh EZPL trực tiếp, hoặc gửi file `.prn` do GoLabel xuất ra.

**GUI**
1. Tab **Lệnh raw** → **Mẫu**: chọn `Godex · EZPL – nhãn test`.
2. Phần mềm tự đặt Ngôn ngữ = EZPL và **bỏ chọn** "Bỏ xuống dòng của editor" (EZPL cần CR LF cuối mỗi dòng — *không được* bật ô này).
3. **▶ Gửi**. Nếu đặt **Chờ phản hồi (ms)** > 0, phản hồi máy in hiện ở khung phải.

Lệnh mẫu:
```
^Q30,3
^W50
^H10
^P1
^L
AD,30,30,1,1,0,0,GODEX EZPL TEST
BQ,30,80,2,6,60,0,1,12345678
E
```

**CLI** (xuống dòng phải viết `<CR><LF>`):
```powershell
printerhub raw --route tcp://192.168.1.52:9100 --text "^Q30,3<CR><LF>^W50<CR><LF>^L<CR><LF>AD,30,30,1,1,0,0,HELLO<CR><LF>E<CR><LF>"
printerhub send --route tcp://192.168.1.52:9100 --file C:\labels\golabel_export.prn
```

### Cách 3 — Qua driver Windows, gửi RAW (Spooler)

Dùng khi máy nối **USB** (hoặc LPT/COM/LAN đã cài driver). Driver Godex chỉ làm "ống dẫn", lệnh EZPL đi nguyên vẹn.

| | |
|---|---|
| Route | `spooler://<Tên máy in trong Windows>` |
| Hai chiều | ❌ (không đọc được `~S,CHECK`) |
| Cần driver | ✅ driver Godex (hoặc "Generic / Text Only") |

**GUI**
1. Tab **Máy in Windows** → **Làm mới** → chọn máy Godex → **Dùng spooler:// (RAW)** (phần mềm tự chọn ngôn ngữ EZPL theo hãng).
2. Tab **Nhãn** → **▶ In qua route**, hoặc tab **Lệnh raw** → **▶ Gửi**.
3. Xem job trong hàng đợi: tab Máy in Windows → **Xem hàng đợi**.

**CLI**
```powershell
printerhub printers                                   # xem tên máy in
printerhub label --route "spooler://Godex G500" --lang EZPL
printerhub jobs  --printer "Godex G500"
```

> Nếu báo lỗi `StartDocPrinter thất bại`: driver từ chối kiểu dữ liệu RAW → thêm `?datatype=RAW` hoặc cài thêm một máy in "Generic / Text Only" trỏ vào cùng cổng USB.

### Cách 4 — Qua driver Windows bằng GDI (in như văn bản/hình)

Driver Godex tự chuyển trang GDI thành ảnh rồi gửi xuống máy. In được **mọi font Windows** (tiếng Việt, tiếng Nhật) mà không cần tải font vào máy in, nhưng chậm hơn và mã vạch là ảnh raster.

**GUI**
1. Tab **Máy in Windows** → chọn máy Godex (hoặc đặt Route = `spooler://Godex G500`).
2. Tab **Nhãn** → soạn nhãn → **In qua driver (GDI)**.
3. Khổ giấy lấy theo `width/height` trong JSON nhãn (dot @ 203 dpi).

> Bản xem trước GDI chỉ vẽ thật Code128; QR và các mã vạch khác là khung giữ chỗ → dùng Cách 1–3 nếu cần QR.
> CLI chưa hỗ trợ GDI (chỉ có trong GUI).

### Cách 5 — Driver passthrough (chèn lệnh EZPL vào văn bản GDI)

| | |
|---|---|
| Route | `passthrough://Godex G500?prefix=${&suffix=}$` |
| Yêu cầu | Driver phải bật tính năng passthrough ⚠ cần xác minh với driver Godex/Seagull |

**GUI:** tab Máy in Windows → chọn máy → **Dùng passthrough://** → tab Lệnh raw → **▶ Gửi**.
Dùng khi chỉ có quyền in qua driver (vd máy in chia sẻ qua máy khác) nhưng vẫn muốn gửi lệnh gốc.

### Cách 6 — Cổng COM (RS-232C / USB-Serial)

| | |
|---|---|
| Route | `serial://COM3?baud=9600&parity=none&databits=8&stopbits=1&handshake=rtscts` |
| Hai chiều | ✅ |
| Lưu ý | baud/handshake phải trùng cài đặt trong máy in ⚠ cần xác minh (mặc định thường 9600, 8N1) |

**GUI:** gõ route vào ô Route → **Trạng thái** để thử → in như Cách 1.
**CLI:**
```powershell
printerhub status --route "serial://COM3?baud=9600" --lang EZPL
printerhub label  --route "serial://COM3?baud=9600" --lang EZPL
```

### Cách 7 — LPR/LPD

Route `lpr://192.168.1.52/lp` — chỉ dùng nếu máy bật dịch vụ LPD ⚠ cần xác minh G500 có LPD hay không. Một chiều.
```powershell
printerhub label --route lpr://192.168.1.52/lp --lang EZPL
```

### Cách 8 — Máy in chia sẻ trong mạng Windows

Máy Godex cắm USB vào máy tính `PC01` và được **Share** với tên `GODEX`:
```powershell
printerhub label --route "file://\\PC01\GODEX" --lang EZPL
printerhub send  --route "file://\\PC01\GODEX" --file job.prn
```
Tương đương `copy /b job.prn \\PC01\GODEX`. Queue chia sẻ phải nhận RAW.

### Cách 9 — Giả lập Zebra (GZPL)

Nếu máy đang ở chế độ tự nhận ngôn ngữ, có thể gửi **ZPL** — dùng chung một nhãn cho cả Godex và Zebra.
```powershell
printerhub label --route tcp://192.168.1.52:9100 --lang GZPL
```
⚠ cần xác minh: font `^A0` và một số lệnh ZPL nâng cao có thể khác máy Zebra thật.

### Cách 10 — In hàng loạt từ CSV

`samples\label.json` chứa biến `{PartNo}`, `{Qty}`, `{Lot}`, `{Name}`; mỗi dòng CSV → 1 nhãn (cột `Copies` = số bản).

- **GUI:** tab Nhãn → **Template {biến}** (hoặc **Mở JSON...**) → **In hàng loạt CSV...** → chọn file CSV.
- **CLI:**
```powershell
printerhub batch --json samples\label.json --csv samples\data.csv --route tcp://192.168.1.52:9100 --lang EZPL
printerhub batch --json samples\label.json --csv samples\data.csv --profile GODEX-G500 --profiles samples\profiles.json
# Chỉ xuất file lệnh để kiểm tra, không in:
printerhub batch --json samples\label.json --csv samples\data.csv --lang EZPL --out out\godex
```

### Cách 11 — In theo profile (tự chuyển route khi lỗi)

- **GUI:** tab **Profiles** → chọn `GODEX-G500` → **▶ In nhãn hiện tại (failover)**.
- **CLI:** `printerhub print --profile GODEX-G500 --profiles samples\profiles.json --json mylabel.json`

### Cách 12 — Thư mục nóng (hot folder) — in tự động

```powershell
printerhub watch --folder C:\PrintIn\Godex --profile GODEX-G500 --profiles samples\profiles.json --template samples\label.json
```
Thả file vào `C:\PrintIn\Godex`:
- `*.json` → in nhãn JSON
- `*.csv` → in hàng loạt theo `--template`
- `*.prn`, `*.txt`, … → gửi raw
File xong chuyển vào `done\`, lỗi vào `error\`.

---

## 4. Bảng tóm tắt

| # | Cách | Route | Hai chiều | Cần driver | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | Raw TCP | `tcp://IP:9100` | ✅ | ❌ | ⭐ chính |
| 2 | Lệnh raw / file .prn | bất kỳ | tuỳ route | tuỳ route | thử lệnh |
| 3 | Spooler RAW | `spooler://Tên` | ❌ | ✅ | ⭐ khi cắm USB |
| 4 | GDI qua driver | (GUI) | ❌ | ✅ | font đặc biệt / logo |
| 5 | Passthrough | `passthrough://Tên` | ❌ | ✅ | ⚠ |
| 6 | COM / RS-232 | `serial://COM3` | ✅ | ❌ | máy không có LAN |
| 7 | LPR | `lpr://IP/lp` | ❌ | ❌ | ⚠ |
| 8 | Chia sẻ Windows | `file://\\PC\Share` | ❌ | ✅ (máy chủ) | dùng chung |
| 9 | GZPL | `tcp://…` + `GZPL` | ✅ | ❌ | dùng chung nhãn Zebra |
| 10–12 | CSV / profile / hot folder | profile | — | — | tự động hoá |

---

## 5. Trạng thái máy in

PrinterHub gửi `~S,CHECK` và hiểu mã trả về:

| Mã | Ý nghĩa | Hiển thị |
|---|---|---|
| 00 | Sẵn sàng | Ready |
| 01 / 02 | Hết giấy / kẹt giấy | Error |
| 03 | Hết ribbon | Error |
| 04 | Mở đầu in | Error |
| 05 | Bộ cuộn đầy | Error |
| 06 | Bộ nhớ đầy | Error |
| 09 | Lỗi cú pháp lệnh | Error |
| 10 | Kẹt dao cắt | Error |
| 20 | Pause | Offline |
| 50 | Đang in | Printing |
| 60 | Đang xử lý dữ liệu | Busy |

- Lần đầu cần bật trả trạng thái tức thời: tab Lệnh raw → mẫu `Godex · ~S,CHECK – trạng thái` (đã gồm `^XSET,IMMEDIATE,1`) → **▶ Gửi** với Chờ phản hồi = 1000.
- Khi in qua route hai chiều, PrinterHub **kiểm tra trạng thái trước khi in** và không gửi nếu máy báo lỗi.
- Nếu máy không trả lời (chưa bật IMMEDIATE), PrinterHub chờ hết `readTimeout` rồi vẫn in → giảm thời gian chờ bằng `?readTimeout=800`.

Theo dõi liên tục:
```powershell
printerhub status --route tcp://192.168.1.52:9100 --lang EZPL --repeat 1000
```

---

## 6. Lệnh EZPL hữu ích (gửi ở tab Lệnh raw)

| Lệnh | Tác dụng |
|---|---|
| `~V` | In trang tự kiểm tra (xem IP, ngôn ngữ, firmware) |
| `~S,SENSOR` | Hiệu chỉnh cảm biến giấy |
| `~S,CHECK` | Đọc trạng thái |
| `^XSET,IMMEDIATE,1` | Bật trả trạng thái tức thời |
| `~MDEL` | Xoá bộ nhớ (form, ảnh, font đã tải) ⚠ |

---

## 7. Tiếng Việt / tiếng Nhật

- **Font nội EZPL (A–H) chỉ có ASCII.** Khi văn bản có dấu/Kanji, PrinterHub sinh lệnh `AT,…` (font TrueType) ⚠ cần xác minh lệnh và font trên G500.
- Cách chắc chắn nhất hiện nay: **Cách 4 (GDI qua driver)** — dùng font Windows.
- Hoặc tải font TTF vào máy bằng **GoLabel II** / tool của Godex, sau đó dùng `fontName` trong JSON nhãn.

---

## 8. Checklist thử với máy thật

1. [ ] Tab **Máy in Windows** thấy máy Godex (nếu cài driver).
2. [ ] Lệnh `~V` in ra trang cấu hình → ghi lại IP, ngôn ngữ, firmware.
3. [ ] `tcp://IP:9100` → **Trạng thái** trả `Ready (00)`.
4. [ ] In **Nhãn mẫu** EZPL: kiểm tra khung, chữ, Code128, QR (lệnh `W` ⚠).
5. [ ] Mở nắp đầu in → **Trạng thái** báo `Mở đầu in (04)`; thử in → PrinterHub phải chặn.
6. [ ] In qua `spooler://` (USB).
7. [ ] In GDI có chữ tiếng Việt.
8. [ ] In hàng loạt `samples\data.csv`.
9. [ ] Rút dây LAN → in theo profile → tự chuyển sang `spooler://`.
10. [ ] Ghi lại lệnh nào sai (QR, ảnh `GW`, chữ `AT`) để chỉnh `EzplLanguage.cs`.

---

## 9. PrinterHub chưa hỗ trợ cho Godex

- EZio SDK (`Ezio32.dll`/`Ezio64.dll`) — USB trực tiếp không cần driver.
- Tải font/ảnh vào bộ nhớ máy và gọi lại form đã lưu.
- Bluetooth/Wi-Fi (G500 cần module tuỳ chọn ⚠) — nếu ghép Bluetooth thành cổng COM ảo thì dùng được Cách 6.
