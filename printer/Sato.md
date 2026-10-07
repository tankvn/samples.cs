# In với máy SATO CL4NX Plus bằng PrinterHub

> Tài liệu hướng dẫn dùng **PrinterHub** (GUI `PrinterHub.exe` và CLI `printerhub`) để in tới máy **SATO CL4NX Plus** thật (máy in nhãn công nghiệp 4 inch).
> Các mục "⚠ cần xác minh" chưa được thử trên máy thật — hãy kiểm tra khi chạy lần đầu.

---

## 1. Thông tin máy CL4NX Plus cần biết

| Hạng mục | Giá trị |
|---|---|
| Độ phân giải | **203 dpi** (8 dot/mm), **305 dpi** (12 dot/mm), **609 dpi** (24 dot/mm) — xem tem máy |
| Khổ in tối đa | 104 mm |
| Cổng chuẩn | USB, LAN, RS-232C, IEEE1284, Bluetooth, NFC; Wi-Fi tuỳ chọn ⚠ tuỳ cấu hình mua |
| Ngôn ngữ | **SBPL** (gốc) · giả lập: **SZPL** (Zebra), SDPL (Datamax), SIPL (Intermec), STCL (Toshiba TEC), SEPL (Eltron) |
| Cổng LAN raw | 3 cổng cấu hình được: **Port1 = 1024**, **Port2 = 1025**, **Port3 = 9100** ⚠ giá trị thường gặp, kiểm tra trong menu máy |
| Giao thức trạng thái | **Status4** (2 cổng hoặc ENQ), Status3, Status5 — chọn trong menu *Interface → LAN → Protocol* ⚠ |
| Kanji | Shift-JIS (CP932), có font Kanji sẵn trong máy |
| Driver Windows | SATO Printer Driver (của SATO) hoặc driver Seagull |

**Trong PrinterHub chọn:** Ngôn ngữ `SBPL` · Encoding `shift_jis`.

---

## 2. Chuẩn bị

### 2.1 Chạy phần mềm

```powershell
cd C:\Github\samples.cs\printer
dotnet build PrinterHub.sln
dotnet run --project src\PrinterHub.App          # GUI
dotnet run --project src\PrinterHub.Cli -- help  # CLI (viết tắt: printerhub)
```

### 2.2 Thử trước bằng máy in giả lập (không cần máy thật)

```powershell
dotnet run --project tools\PrinterHub.Simulator
```
Simulator mở cổng **9100**, **1024/1025** (SATO Status4) và LPD **5515**; trả lời ENQ giống SATO, giảm dần số nhãn còn lại khi "in". Phím `p` = hết giấy, `h` = mở đầu in, `r` = bình thường.
Mọi lệnh dưới đây thay IP bằng `127.0.0.1` là chạy được với simulator.

### 2.3 Kiểm tra cài đặt LAN trên máy SATO

Trên bảng điều khiển: **Settings → Interface → Network (LAN)** ⚠ tên menu có thể khác theo firmware:
1. Ghi lại **IP Address**.
2. **Port1 / Port2 / Port3** (vd 1024 / 1025 / 9100).
3. **Protocol (Flow control)**: `Status4` để dùng route `sato4://` hai cổng; `Status4 ENQ` / `Status3` / `Status5` dùng với `tcp://`.
4. Có thể xem/sửa trên trang web **WebConfig**: `http://<IP>`.

### 2.4 Tìm máy trên mạng

- **GUI:** tab **Tìm máy in mạng** → **▶ Quét** → dòng có cổng `1024,1025,9100` được nhận diện **"SATO Status4 (sato4://)"** → **Dùng route đã chọn** (tự đặt `sato4://IP?data=1024&status=1025` và ngôn ngữ SBPL).
- **CLI:** `printerhub discover --subnet 192.168.1 --ports 1024,1025,9100,515,80`

### 2.5 Profile mẫu

```json
{
  "name": "SATO-CL4NX",
  "brand": "SATO",
  "model": "CL4NX Plus",
  "dpi": 305,
  "language": "SBPL",
  "encoding": "shift_jis",
  "routes": [
    "sato4://192.168.1.50?data=1024&status=1025",
    "tcp://192.168.1.50:9100",
    "spooler://SATO CL4NX Plus 305dpi"
  ],
  "retryCount": 2,
  "retryDelayMs": 1000,
  "languageOptions": { "kanjiFont": "K9B", "jobId": "01" }
}
```

**Tuỳ chọn SBPL (`languageOptions` hoặc `--opt` trong CLI):**

| Tuỳ chọn | Mặc định | Ý nghĩa |
|---|---|---|
| `textFont` | `XM` | Font bitmap cho chữ ASCII (`XU`, `XS`, `XM`, `XB`, `XL`, `OA`, `OB`) |
| `textMode` | `bitmap` | `scalable` → dùng font co giãn `ESC $ … ESC $=` |
| `kanjiFont` | `K9B` | Lệnh font Kanji ⚠ cần xác minh mã font theo máy |
| `kanjiCode` | (không) | Lệnh chọn bảng mã Kanji phát sau `ESC A`, vd `KC1` ⚠ |
| `jobId` | (không) | 2 chữ số → `ESC ID`, máy trả lại trong Status4 |
| `labelSize` | `true` | Phát `ESC A1` (kích thước nhãn từ JSON) |
| `nonStandardCodes` | `false` | `true` → thay STX/ETX/ESC bằng `{` `}` `^` (khi máy đặt "Non-standard protocol") |

> Máy **305 dpi**: đặt `"dpi": 305` trong profile **và** trong JSON nhãn (toạ độ tính bằng dot: 1 mm ≈ 12 dot). CLI nhãn mẫu: `--dpi 305`.

---

## 3. Tất cả các cách in bằng PrinterHub

### Cách 1 — SATO Status4 hai cổng `sato4://` (khuyến nghị)

Cổng **data (1024)** nhận lệnh in, cổng **status (1025)** trả trạng thái — đọc được trạng thái **ngay cả khi máy đang nhận/in dữ liệu**.

| | |
|---|---|
| Route | `sato4://192.168.1.50?data=1024&status=1025` |
| Cài đặt máy | Protocol = **Status4** (2 cổng) |
| Hai chiều | ✅ trạng thái + **số nhãn còn lại** + Job ID |
| Cần driver | ❌ |

**GUI**
1. Route `sato4://192.168.1.50?data=1024&status=1025` · Ngôn ngữ `SBPL` · Encoding `shift_jis`.
2. **Trạng thái** → hiện `Ready | Remaining=0 | JobId=01 | Code='A'` (xanh).
3. Tab **Nhãn** → **Nhãn mẫu** → xem lệnh SBPL ở khung dưới → nhập **Số bản** → **▶ In qua route**.
4. Bấm **Trạng thái** lại khi đang in → `Printing | Remaining=n`.

**CLI**
```powershell
printerhub label  --route "sato4://192.168.1.50?data=1024&status=1025" --lang SBPL --copies 10 --dpi 305
printerhub status --route "sato4://192.168.1.50?data=1024&status=1025" --lang SBPL --repeat 1000
```

### Cách 2 — Raw TCP một cổng (9100 hoặc 1024) + ENQ

| | |
|---|---|
| Route | `tcp://192.168.1.50:9100` |
| Cài đặt máy | Port3 / Protocol **Status4 ENQ**, Status3 hoặc Status5 ⚠ |
| Hai chiều | ✅ PrinterHub gửi `ENQ` (0x05) trên cùng kết nối |

```powershell
printerhub label  --route tcp://192.168.1.50:9100 --lang SBPL
printerhub status --route tcp://192.168.1.50:9100 --lang SBPL
```
Dùng khi chỉ mở được 1 cổng qua tường lửa, hoặc máy đặt chế độ một cổng.

### Cách 3 — Lệnh SBPL tự viết (tab Lệnh raw)

**GUI:** tab **Lệnh raw** → **Mẫu** có sẵn:

| Mẫu | Nội dung |
|---|---|
| `SATO · SBPL – nhãn test` | chữ XM phóng to, Code128, chữ XS |
| `SATO · SBPL – ký hiệu manual <A>` | viết kiểu tài liệu SATO `<A><V>0100<H>0100…` |
| `SATO · SBPL – Kanji (Shift-JIS)` | `<ESC>K9B部品テスト` |
| `SATO · SBPL – QR code` | `<ESC>2D30,M,06,1,0<ESC>DN0014,…` |
| `SATO · Status4 – ENQ` | hỏi trạng thái |
| `SATO · Huỷ job – CAN` | huỷ job đang in |
| `SATO · DC2 PG – trạng thái mở rộng` | `<DC2>PG` ⚠ |

Ký hiệu trong editor:
- `<STX>` `<ETX>` `<ESC>` `<ENQ>` `<CAN>` `<DC2>` `<CR>` `<LF>` `<0x1B>` → byte điều khiển.
- Tích **"Ký hiệu SATO <A>=ESC A"** → viết đúng như manual SATO: `<A>` = `ESC A`, `<H>` = `ESC H`, `<XM>` = `ESC XM`… (viết `<<` nếu cần ký tự `<` thật).
- Để **bật** "Bỏ xuống dòng của editor" (SBPL không cần CR LF; xuống dòng chỉ để dễ đọc).
- **Xem hex** để kiểm tra byte trước khi gửi; **Chờ phản hồi** = 1000 khi gửi ENQ.

Ví dụ (bật ký hiệu SATO):
```
<STX><A>
<V>0100<H>0100<L>0202<XM>SATO CL4NX PLUS
<V>0200<H>0100<BG>02100>H12345678
<V>0350<H>0100<K9B>部品テスト
<Q>1<Z><ETX>
```

**CLI**
```powershell
# Kiểu tài liệu SATO (--sbpl), Kanji Shift-JIS
printerhub raw --route tcp://192.168.1.50:9100 --sbpl --encoding shift_jis --text "<STX><A><V>100<H>100<L>0202<K9B>部品<Q>1<Z><ETX>"
# Hỏi trạng thái
printerhub raw --route tcp://192.168.1.50:9100 --text "<ENQ>" --wait 1000
# Huỷ job
printerhub raw --route tcp://192.168.1.50:9100 --text "<CAN>"
# Gửi file .prn có sẵn (Multi LABELIST, BarTender "print to file"...)
printerhub send --route "sato4://192.168.1.50" --file C:\labels\label.prn
```

### Cách 4 — Qua driver SATO, gửi RAW (Spooler)

Dùng khi máy cắm **USB** / IEEE1284 / COM hoặc đã cài trong Windows. Lệnh SBPL đi nguyên vẹn qua driver.

| | |
|---|---|
| Route | `spooler://SATO CL4NX Plus 305dpi` |
| Hai chiều | ❌ |
| Cần driver | ✅ SATO Printer Driver (hoặc "Generic / Text Only" cùng cổng) |

**GUI:** tab **Máy in Windows** → **Làm mới** → chọn máy SATO (cột Hãng = SATO) → **Dùng spooler:// (RAW)** (tự chọn SBPL) → in ở tab Nhãn / Lệnh raw → **Xem hàng đợi**.

**CLI**
```powershell
printerhub printers
printerhub label --route "spooler://SATO CL4NX Plus 305dpi" --lang SBPL --dpi 305
printerhub jobs  --printer "SATO CL4NX Plus 305dpi"
```
> `StartDocPrinter thất bại` → driver không nhận RAW → thêm máy in "Generic / Text Only" trỏ cùng cổng USB và dùng tên đó.

### Cách 5 — GDI qua driver SATO (in như hình)

Driver SATO chuyển trang GDI sang lệnh in ảnh. In được mọi font Windows (tiếng Việt, Kanji đẹp), logo; chậm hơn SBPL, mã vạch là ảnh.

**GUI:** chọn máy ở tab Máy in Windows (hoặc Route `spooler://SATO…`) → tab **Nhãn** → **In qua driver (GDI)**.
Kích thước trang = `width × height` (dot) ÷ `dpi` trong JSON nhãn. CLI chưa hỗ trợ.

> Xem trước GDI vẽ thật Code128; QR/mã khác là khung giữ chỗ → dùng SBPL nếu cần QR.

### Cách 6 — Driver passthrough / "Command Font" của driver SATO ⚠

Driver SATO có tính năng **Command Font**: văn bản in bằng font lệnh sẽ được gửi nguyên thành lệnh SBPL.

| | |
|---|---|
| Route | `passthrough://SATO CL4NX Plus 305dpi?prefix=&suffix=&font=<tên Command Font>` |
| Yêu cầu | Bật/cấu hình Command Font trong Printing Preferences của driver SATO ⚠ cần xác minh tên font và chuỗi đầu/cuối |

**GUI:** tab Máy in Windows → chọn máy → **Dùng passthrough://** → sửa route thêm `prefix`, `suffix`, `font` theo cài đặt driver → gửi ở tab Lệnh raw.
Dùng khi bắt buộc in qua driver (máy chủ in) nhưng vẫn muốn lệnh SBPL gốc.

### Cách 7 — Cổng COM: RS-232C / Bluetooth

- **RS-232C:** `serial://COM3?baud=9600&parity=none&databits=8&stopbits=1&handshake=rtscts`
  (baud, parity, kiểu bắt tay phải trùng menu *Interface → RS-232C* của máy; SATO "READY/BUSY" ≈ `rtscts` hoặc `dsrdtr` tuỳ cáp ⚠).
- **Bluetooth:** ghép đôi trong Windows → cổng COM ảo (Outgoing) → `serial://COM8`.

```powershell
printerhub status --route "serial://COM3?baud=9600&handshake=rtscts" --lang SBPL
printerhub label  --route "serial://COM3?baud=9600&handshake=rtscts" --lang SBPL
```
Hai chiều ✅ (ENQ trên cùng cổng).

### Cách 8 — LPR/LPD

```powershell
printerhub label --route lpr://192.168.1.50/lp --lang SBPL
```
Một chiều. Hợp với hệ thống chỉ cho phép cổng 515.

### Cách 9 — Máy in chia sẻ Windows / cổng LPT

```powershell
printerhub label --route "file://\\PC01\SATO_CL4NX" --lang SBPL     # máy chia sẻ trên PC01
printerhub label --route "file://\\.\LPT1" --lang SBPL              # IEEE1284 (cổng song song)
```

### Cách 10 — Giả lập Zebra (SZPL)

Khi máy bật giả lập ZPL (hoặc tự nhận ngôn ngữ), dùng chung nhãn với máy Zebra:
```powershell
printerhub label --route tcp://192.168.1.50:9100 --lang SZPL --encoding utf-8
```
⚠ cần xác minh: bật SZPL trong menu *Application* của máy; font và một số lệnh có thể khác Zebra thật. Trạng thái dùng `~HS` thay vì ENQ.

### Cách 11 — In hàng loạt từ CSV

`samples\label.json` dùng biến `{Title}`, `{PartNo}`, `{Qty}`, `{Lot}`, `{Name}`; mỗi dòng CSV là 1 nhãn, cột `Copies` = số bản. CSV **Shift-JIS hoặc UTF-8** đều tự nhận.

- **GUI:** tab Nhãn → **Template {biến}** / **Mở JSON...** → **In hàng loạt CSV...**
- **CLI:**
```powershell
printerhub batch --json samples\label.json --csv samples\data.csv --route "sato4://192.168.1.50" --lang SBPL
printerhub batch --json samples\label.json --csv samples\data.csv --profile SATO-CL4NX --profiles samples\profiles.json
printerhub batch --json samples\label.json --csv samples\data.csv --lang SBPL --out out\sato   # chỉ xuất file lệnh
```

### Cách 12 — In theo profile (tự chuyển route khi lỗi)

- **GUI:** tab **Profiles** → `SATO-CL4NX` → **▶ In nhãn hiện tại (failover)**.
- **CLI:** `printerhub print --profile SATO-CL4NX --profiles samples\profiles.json --json mylabel.json`

Thứ tự trong profile mẫu: `sato4://` → `tcp://9100` → `spooler://`. Nếu máy báo lỗi (hết giấy, mở đầu in…) PrinterHub **không gửi** và thử lại / chuyển route.

### Cách 13 — Thư mục nóng (in tự động)

```powershell
printerhub watch --folder C:\PrintIn\Sato --profile SATO-CL4NX --profiles samples\profiles.json --template samples\label.json
```
Thả `*.json` (nhãn), `*.csv` (hàng loạt), `*.prn` / `*.txt` (lệnh SBPL raw). Xong → `done\`, lỗi → `error\`.

### Cách 14 — In PDF trực tiếp ⚠

CL4NX Plus có firmware **PDF Direct Print** (tuỳ chọn). Khi đã cài:
```powershell
printerhub send --route tcp://192.168.1.50:9100 --file label.pdf
```
⚠ cần xác minh firmware, cổng và cách bật trên máy.

---

## 4. Bảng tóm tắt

| # | Cách | Route | Hai chiều | Cần driver | Khuyến nghị |
|---|---|---|---|---|---|
| 1 | Status4 hai cổng | `sato4://IP?data=1024&status=1025` | ✅ + số nhãn còn lại | ❌ | ⭐⭐ chính |
| 2 | Raw TCP + ENQ | `tcp://IP:9100` | ✅ | ❌ | ⭐ |
| 3 | SBPL tự viết / file .prn | bất kỳ | tuỳ route | tuỳ route | thử lệnh |
| 4 | Spooler RAW | `spooler://SATO…` | ❌ | ✅ | ⭐ khi cắm USB |
| 5 | GDI | (GUI) | ❌ | ✅ | font đặc biệt, logo |
| 6 | Command Font | `passthrough://SATO…` | ❌ | ✅ | ⚠ |
| 7 | RS-232C / Bluetooth | `serial://COMx` | ✅ | ❌ | dây chuyền cũ |
| 8 | LPR | `lpr://IP/lp` | ❌ | ❌ | khi chỉ mở 515 |
| 9 | Chia sẻ / LPT | `file://\\PC\Share`, `file://\\.\LPT1` | ❌ | tuỳ | dùng chung |
| 10 | SZPL | `tcp://…` + `SZPL` | ✅ (~HS) | ❌ | dùng chung nhãn Zebra |
| 11–13 | CSV / profile / hot folder | profile | — | — | tự động hoá |
| 14 | PDF Direct | `tcp://IP:9100` | ❌ | ❌ | ⚠ |

---

## 5. Trạng thái máy in (Status4)

PrinterHub gửi `ENQ`, nhận `STX` + Job ID (2) + **mã trạng thái** (1) + **số nhãn còn lại** (6) + tên job (16) + `ETX`.

| Mã | Ý nghĩa | Hiển thị |
|---|---|---|
| `0`–`3` | Offline | Offline |
| `A`–`D` | Online, chờ dữ liệu | Ready |
| `G`–`J` | Đang in | Printing · Remaining=n |
| `M`–`P` | Chờ lấy nhãn (bóc/cắt) | WaitingForTakeOut |
| `S`–`V` | Đang phân tích dữ liệu | Busy |
| Vị trí trong nhóm: +1 / +2 / +3 | Ribbon sắp hết / bộ đệm sắp đầy / cả hai | cảnh báo (màu vàng) |
| `a` | Tràn bộ đệm | Error |
| `b` | Mở đầu in | Error · HeadOpen |
| `c` | Hết giấy | Error · PaperOut |
| `d` | Hết ribbon | Error · RibbonOut |
| `e` / `f` / `g` | Lỗi giấy / cảm biến / đầu in | Error |
| `h` | Mở nắp | Error · CoverOpen |
| `j` | Lỗi dao cắt | Error |
| `k` | Lỗi khác | Error |

⚠ Bảng mã lỗi chữ thường cần đối chiếu mục *Return Status* trong *CL4NX Plus Programming Reference*. Nếu khác, sửa `SatoStatus.Decode` trong `src\PrinterHub.Languages\Sato\SbplLanguage.cs`.

- Đặt `jobId` (vd `"01"`) để biết máy đang báo cho job nào.
- Theo dõi tiến độ in: `printerhub status --route "sato4://IP" --lang SBPL --repeat 500`.
- Huỷ job đang in: gửi `<CAN>` (Cách 3).

---

## 6. Lệnh SBPL hữu ích

| Lệnh (ký hiệu SATO) | Tác dụng |
|---|---|
| `<STX><A> … <Z><ETX>` | Khung một job |
| `<A1>VVVVHHHH` | Kích thước nhãn (cao, rộng – dot) |
| `<V>nnnn` `<H>nnnn` | Vị trí dọc / ngang |
| `<L>aabb` | Phóng to chữ (ngang, dọc) |
| `<XS>` `<XM>` `<XL>`… | Font bitmap |
| `<%>n` | Xoay 0/90/180/270 (n = 0–3) |
| `<BG>aabbb>H…` | Code128 |
| `<B>1aabbb*…*` | Code39 |
| `<2D30>,M,05,1,0<DN>nnnn,…` | QR code ⚠ |
| `<FW>aabbVccccHdddd` | Khung chữ nhật |
| `<GH>aaabbb…` | Ảnh (hex) |
| `<Q>n` | Số bản |
| `<ID>nn` | Job ID |
| `<WK>tên` | Tên job |
| `<#E>n` | Độ đậm (1–5) |
| `<ENQ>` | Hỏi trạng thái |
| `<CAN>` | Huỷ |

---

## 7. Tiếng Nhật / tiếng Việt

- **Tiếng Nhật:** Encoding `shift_jis`. Dòng chữ có ký tự đa byte được PrinterHub tự in bằng font Kanji (`kanjiFont`, mặc định `K9B`) và mã hoá CP932. Nếu ra ký tự sai: thử `kanjiCode` (vd `KC1`) hoặc đổi `kanjiFont` ⚠.
- **Tiếng Việt:** font Kanji/bitmap SATO không có dấu tiếng Việt. Cách chắc chắn: **GDI qua driver** (Cách 5). Hoặc đặt `"style": "Unicode"` cho dòng chữ trong JSON (dùng font co giãn `ESC $`) kèm Encoding `utf-8` ⚠ cần xác minh máy hỗ trợ UTF-8 cho font co giãn.
- CSV dữ liệu có thể là Shift-JIS hoặc UTF-8 — PrinterHub tự nhận.

---

## 8. Checklist thử với máy thật

1. [ ] Ghi IP, Port1/2/3, Protocol trong menu Interface.
2. [ ] `discover` thấy cổng 1024/1025/9100.
3. [ ] `sato4://` → **Trạng thái** = `Ready`, `Code='A'`.
4. [ ] In **Nhãn mẫu** (đúng dpi): kiểm tra khung, chữ XM phóng to, Code128, QR, dòng Kanji `部品テスト`.
5. [ ] In 20 bản, bấm **Trạng thái** liên tục → `Remaining` giảm dần.
6. [ ] Mở đầu in → Trạng thái báo `Mở đầu in ('b')` → thử in, PrinterHub phải chặn.
7. [ ] Hết giấy → `'c'`.
8. [ ] Gửi `<CAN>` khi đang in → máy dừng.
9. [ ] `tcp://IP:9100` + ENQ.
10. [ ] `spooler://` (USB) và **In qua driver (GDI)** có chữ tiếng Việt.
11. [ ] Batch CSV Shift-JIS.
12. [ ] Rút dây LAN → in theo profile → tự chuyển sang `spooler://`.
13. [ ] Ghi lại lệnh sai (Kanji, QR, ảnh `GH`, mã trạng thái) để sửa `SbplLanguage.cs`.

---

## 9. PrinterHub chưa hỗ trợ cho SATO

- **Multi LABELIST Component** (SDK .NET của SATO) — USB hai chiều không cần driver.
- **FTP** tới máy in, **SNMP**, **WebConfig** tự động.
- Lưu/gọi format trong máy (`<YS>`/`<YR>`, `<&S>`/`<&R>`) — gửi được bằng tab Lệnh raw nhưng chưa có giao diện riêng.
- **AEP** (ứng dụng chạy trên máy in), **SATO Online Services**, NFC.
- Tín hiệu ngoài EXT/PLC.
