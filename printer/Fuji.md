# In với máy Fuji Xerox DocuCentre-VI C2271 bằng PrinterHub

> Tài liệu hướng dẫn dùng **PrinterHub** (GUI `PrinterHub.exe` và CLI `printerhub`) để in tới máy photocopy màu đa chức năng **Fuji Xerox DocuCentre-VI C2271** (A3, nay là FUJIFILM Business Innovation).
> Đây là **máy in văn phòng** (in trang A4/A3), không phải máy in nhãn: không dùng SBPL/ZPL/EZPL mà dùng **PDF / PCL / PostScript / ART EX** qua driver hoặc qua mạng.
> Các mục "⚠ cần xác minh" chưa được thử trên máy thật.

---

## 1. Thông tin máy cần biết

| Hạng mục | Giá trị |
|---|---|
| Loại | MFP màu A3 (copy / in / scan) |
| Độ phân giải in | tối đa 1200 × 2400 dpi ⚠ |
| Ngôn ngữ in (PDL) | Chuẩn: **ART EX** (driver gốc Fuji Xerox) và/hoặc **PCL6/PCL5** ⚠ tuỳ cấu hình bán ở Việt Nam · Tuỳ chọn: **Adobe PostScript 3** (kit) |
| In trực tiếp file | PDF / TIFF / JPEG / XPS / DocuWorks — PDF thường cần kit PostScript ⚠ cần xác minh |
| Giao thức mạng | **Port9100**, **LPD (515)**, **IPP (631)**, **WSD**, SMB ⚠ — bật/tắt trong **CentreWare Internet Services** (`http://<IP>`) |
| Cổng | Ethernet 1000BASE-T, USB 2.0; Wi-Fi tuỳ chọn ⚠ |
| Driver Windows | "FX DocuCentre-VI C2271 PCL 6" hoặc "… ART EX" (và "… PS" nếu có kit) |
| Trạng thái | `@PJL INFO STATUS` ⚠ cần xác minh máy có trả lời PJL; SNMP (PrinterHub chưa hỗ trợ) |

**Ngôn ngữ chọn trong PrinterHub:**
- `PJL` — PrinterHub bọc file PDF/PCL/PS bằng header PJL (tên job, số bản, khổ giấy, in 2 mặt).
- `RAW` — gửi nguyên file đã có sẵn header (vd file `.prn` do driver tạo ra).

---

## 2. Chuẩn bị

### 2.1 Trên máy in (CentreWare Internet Services)

1. Mở trình duyệt `http://<IP máy in>` → đăng nhập admin (tài khoản mặc định ghi trong Administrator Guide ⚠).
2. **Properties → Connectivity → Port Settings**: bật **Port9100**, **LPD**, **IPP** (tuỳ cách sẽ dùng).
3. In **Configuration Report** từ bảng điều khiển để xem: IP, các PDL đã cài (PCL / PostScript / ART EX), PDF Direct Print có hay không.

### 2.2 Chạy phần mềm

```powershell
cd C:\Github\samples.cs\printer
dotnet build PrinterHub.sln
dotnet run --project src\PrinterHub.App          # GUI
dotnet run --project src\PrinterHub.Cli -- help  # CLI (viết tắt: printerhub)
```

### 2.3 Tìm máy trên mạng

- **GUI:** tab **Tìm máy in mạng** → Cổng `9100,515,631,80` → **▶ Quét** → máy Fuji có 9100 + 515 + 631, cột "Nhận diện" hiện tiêu đề trang web CentreWare.
- **CLI:** `printerhub discover --subnet 192.168.1 --ports 9100,515,631,80`

### 2.4 Profile mẫu

```json
{
  "name": "FUJI-C2271",
  "brand": "FUJIFILM",
  "model": "DocuCentre-VI C2271",
  "dpi": 600,
  "language": "PJL",
  "encoding": "utf-8",
  "routes": [
    "tcp://192.168.1.60:9100",
    "lpr://192.168.1.60/lp",
    "spooler://FX DocuCentre-VI C2271 PCL 6"
  ],
  "retryCount": 1,
  "retryDelayMs": 2000,
  "languageOptions": { "language": "PDF", "paper": "A4", "duplex": "false" }
}
```

> Với route `spooler://` hãy dùng file đã đúng định dạng của driver (xem Cách 4); không nên bọc PJL hai lần.

---

## 3. Tất cả các cách in bằng PrinterHub

### Cách 1 — Port9100: gửi PDF trực tiếp (PDF Direct Print)

| | |
|---|---|
| Route | `tcp://<IP>:9100` |
| Ngôn ngữ | `PJL` + định dạng `PDF` |
| Yêu cầu | Máy có tính năng in PDF trực tiếp (thường cần **PostScript kit**) ⚠ cần xác minh trên Configuration Report |
| Hai chiều | ✅ `@PJL INFO STATUS` (nếu máy hỗ trợ) |

**CLI**
```powershell
printerhub send --route tcp://192.168.1.60:9100 --lang PJL --format PDF --file C:\docs\report.pdf --copies 2
# In 2 mặt, khổ A4
printerhub send --route tcp://192.168.1.60:9100 --lang PJL --format PDF --opt duplex=true --opt paper=A4 --file report.pdf
```

Dữ liệu PrinterHub gửi đi:
```
<ESC>%-12345X@PJL
@PJL JOB NAME="report.pdf"
@PJL SET COPIES=2
@PJL ENTER LANGUAGE=PDF
%PDF-1.7 …(nội dung file)…
<ESC>%-12345X@PJL EOJ
<ESC>%-12345X
```

**GUI:** chưa có nút gửi file nhị phân (PDF/PCL) — tab Lệnh raw chỉ dành cho lệnh dạng văn bản. Dùng CLI, hot folder (Cách 8) hoặc profile (Cách 7).

### Cách 2 — Port9100: gửi PCL hoặc PostScript

Khi máy không có PDF Direct, chuyển tài liệu sang **PCL** hoặc **PostScript** trước:
- In từ ứng dụng bất kỳ ra file: chọn driver Fuji → tích **Print to file** → được file `.prn` (PCL6 / ART EX / PS tuỳ driver).
- Hoặc dùng phần mềm khác xuất `.pcl` / `.ps`.

```powershell
# File .prn do driver tạo ra đã có header → gửi RAW
printerhub send --route tcp://192.168.1.60:9100 --lang RAW --file C:\out\report.prn
# File PCL/PS "trần" → bọc PJL
printerhub send --route tcp://192.168.1.60:9100 --lang PJL --format PCL        --file report.pcl
printerhub send --route tcp://192.168.1.60:9100 --lang PJL --format POSTSCRIPT --file report.ps
```
⚠ PostScript chỉ in được khi đã có kit PS; PCL chỉ khi máy có PCL.

### Cách 3 — LPR/LPD (cổng 515)

Cùng dữ liệu như Cách 1–2 nhưng qua giao thức LPD — hợp với mạng/tường lửa chỉ mở 515.
```powershell
printerhub send --route lpr://192.168.1.60/lp --lang PJL --format PDF --file report.pdf
printerhub send --route lpr://192.168.1.60/lp --lang RAW --file report.prn
```
Tên queue thường không quan trọng với máy Fuji (`lp`, `auto`…) ⚠. Một chiều.

### Cách 4 — Qua driver Windows, gửi RAW (Spooler)

Dùng khi máy đã được cài trong Windows (TCP/IP port, WSD, USB) và bạn có file đúng định dạng của driver đó (`.prn` từ "Print to file").

| | |
|---|---|
| Route | `spooler://FX DocuCentre-VI C2271 PCL 6` |
| Hai chiều | ❌ |

**GUI:** tab **Máy in Windows** → **Làm mới** → chọn máy Fuji (cột Hãng = FUJIFILM) → **Dùng spooler:// (RAW)** để lấy route, rồi gửi file bằng CLI bên dưới; **Xem hàng đợi** để theo dõi job.

**CLI**
```powershell
printerhub printers
printerhub send --route "spooler://FX DocuCentre-VI C2271 PCL 6" --lang RAW --file report.prn
printerhub jobs --printer "FX DocuCentre-VI C2271 PCL 6"
```
> Lỗi `StartDocPrinter thất bại` → driver (đặc biệt driver v4 / Microsoft IPP Class Driver) từ chối RAW → dùng Cách 1–3.

### Cách 5 — GDI qua driver (in trang do PrinterHub vẽ)

Tab **Nhãn** vẽ được trang bằng GDI rồi gửi qua driver Fuji (driver lo việc chuyển sang ART EX/PCL). Dùng cho phiếu/nhãn khổ lớn in trên giấy A4: text, khung, đường kẻ, Code128, logo.

**GUI**
1. Tab Máy in Windows → chọn máy Fuji (hoặc Route `spooler://FX DocuCentre-VI C2271…`).
2. Tab Nhãn → soạn JSON với kích thước trang A4, ví dụ `"dpi": 100, "width": 827, "height": 1169` (210 × 297 mm).
3. **In qua driver (GDI)**.

Ưu: không cần biết PDL của máy, in được font tiếng Việt/Nhật của Windows. Nhược: chỉ là bố cục đơn giản (không phải trình soạn thảo văn bản); QR là khung giữ chỗ.

### Cách 6 — Máy in chia sẻ Windows

Máy Fuji được chia sẻ trên máy chủ in `PRINTSRV` với tên `C2271`:
```powershell
printerhub send --route "file://\\PRINTSRV\C2271" --lang RAW --file report.prn
```
Queue chia sẻ phải nhận dữ liệu RAW đúng PDL của driver.

### Cách 7 — In theo profile (failover TCP → LPR → driver)

```powershell
printerhub print --profile FUJI-C2271 --profiles samples\profiles.json --file C:\docs\report.pdf --format PDF
```
**GUI:** tab **Profiles** → `FUJI-C2271` → **Áp dụng (route/ngôn ngữ)**. (Nút "In nhãn hiện tại" in nhãn, không dùng cho PDF.)

### Cách 8 — Thư mục nóng (in PDF tự động)

```powershell
printerhub watch --folder C:\PrintIn\Fuji --profile FUJI-C2271 --profiles samples\profiles.json
```
Thả file vào thư mục:

| Đuôi file | PrinterHub gửi |
|---|---|
| `.pdf` | PJL + `ENTER LANGUAGE=PDF` |
| `.ps` | PJL + `ENTER LANGUAGE=POSTSCRIPT` |
| `.pcl` | PJL + `ENTER LANGUAGE=PCL` |
| khác (`.prn`…) | PJL với ngôn ngữ trong `languageOptions.language` |

File xong chuyển vào `done\`, lỗi vào `error\`. Phù hợp để hệ thống ERP/MES xuất PDF ra thư mục và tự in.

### Cách 9 — Lệnh PJL tự viết (tab Lệnh raw)

Mẫu có sẵn: `FUJIFILM · PJL INFO STATUS`, `FUJIFILM · PJL INFO ID`, `FUJIFILM · PCL – trang text`.
Route `tcp://IP:9100`, **Chờ phản hồi** = 2000 → **▶ Gửi**.

```powershell
printerhub raw --route tcp://192.168.1.60:9100 --wait 2000 --text "<ESC>%-12345X@PJL INFO ID<CR><LF><ESC>%-12345X"
```

---

## 4. Bảng tóm tắt

| # | Cách | Route | Định dạng | Hai chiều | Cần driver | Khuyến nghị |
|---|---|---|---|---|---|---|
| 1 | Port9100 + PDF | `tcp://IP:9100` | PDF | ✅ ⚠ | ❌ | ⭐ nếu có PDF Direct |
| 2 | Port9100 + PCL/PS/.prn | `tcp://IP:9100` | PCL / PS / ART EX | ✅ ⚠ | ❌ (cần driver để tạo file) | ⭐ |
| 3 | LPR | `lpr://IP/lp` | như 1–2 | ❌ | ❌ | khi chỉ mở 515 |
| 4 | Spooler RAW | `spooler://FX …` | `.prn` của driver | ❌ | ✅ | máy đã cài sẵn |
| 5 | GDI | (GUI) | trang vẽ | ❌ | ✅ | phiếu/nhãn A4 |
| 6 | Chia sẻ | `file://\\SRV\Share` | `.prn` | ❌ | ✅ (máy chủ) | qua máy chủ in |
| 7 | Profile | profile | — | — | — | failover |
| 8 | Hot folder | profile | PDF/PS/PCL | — | — | ⭐ tự động hoá |
| 9 | PJL tự viết | `tcp://IP:9100` | PJL | ✅ ⚠ | ❌ | chẩn đoán |

---

## 5. Trạng thái máy in

PrinterHub gửi `@PJL INFO STATUS` và đọc `CODE=…`:

| CODE | Ý nghĩa | Hiển thị |
|---|---|---|
| 10001 / 10002 | Sẵn sàng | Ready |
| 10003–10006 | Khởi động / làm nóng / bận | Busy |
| 10023 / 10024 | Đang in | Printing |
| 41xxx | Hết giấy | Error · PaperOut |
| 42xxx | Kẹt giấy | Error · PaperJam |
| 4xxxx khác | Lỗi cần xử lý | Error |
| `ONLINE=FALSE` | Offline | Offline |

```powershell
printerhub status --route tcp://192.168.1.60:9100 --lang PJL
```
**GUI:** Route `tcp://IP:9100`, Ngôn ngữ `PJL` → **Trạng thái**.

⚠ Máy Fuji Xerox có thể không trả lời PJL hoặc trả mã riêng; nếu chỉ thấy "Không có phản hồi (timeout)", hãy xem trạng thái/mực trên CentreWare. Đọc trạng thái qua SNMP (mức mực, khay giấy) chưa có trong PrinterHub.

> Khi route hai chiều, PrinterHub hỏi trạng thái **trước khi in**; nếu máy không trả lời PJL, mỗi lần in sẽ chờ `readTimeout` (mặc định 2 giây) → đặt `tcp://IP:9100?readTimeout=500` để in nhanh hơn.

---

## 6. Checklist thử với máy thật

1. [ ] In Configuration Report → ghi IP, PDL đã cài (PCL? PS? PDF Direct?).
2. [ ] CentreWare: Port9100, LPD bật.
3. [ ] `printerhub discover` thấy cổng 9100/515/631.
4. [ ] `@PJL INFO ID` / `INFO STATUS` có trả lời không.
5. [ ] Gửi một PDF nhỏ qua Cách 1 → in đúng? (nếu ra trang ký tự rác → máy không có PDF Direct → dùng Cách 2).
6. [ ] Tạo `.prn` bằng driver PCL6 (Print to file) → gửi Cách 2 và Cách 4.
7. [ ] `--copies 2`, `--opt duplex=true` có hiệu lực.
8. [ ] LPR (Cách 3).
9. [ ] GDI (Cách 5) trang A4 có chữ tiếng Việt.
10. [ ] Hot folder với PDF.

---

## 7. PrinterHub chưa hỗ trợ cho máy Fuji

- **IPP/IPPS** (in PDF qua cổng 631, đọc trạng thái chuẩn).
- **SNMP** (mức mực, khay giấy, lỗi chi tiết).
- In tài liệu Office (Word/Excel) trực tiếp — hiện phải xuất PDF hoặc "Print to file" trước.
- In bảo mật (Secure/Private Print có mật khẩu), mã tài khoản (Accounting), chọn khay/đầu ra — cần lệnh PJL riêng của Fuji Xerox ⚠.
- Email print, in từ USB, Mopria/AirPrint — đây là tính năng của máy, không qua PrinterHub.
