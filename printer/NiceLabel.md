# Kế hoạch: đọc file NiceLabel `.nlbl`

> File cần đọc: `printer\50556(V9-000190-00V).nlbl` (11 749 byte, ngày 18/02/2024)
> Mục tiêu: lấy được **nội dung nhãn** (kích thước, máy in, biến, đối tượng text/barcode/QR, giá trị mặc định) để
> (1) in lại bằng **PrinterHub**, hoặc (2) chuyển sang **JSON nhãn của PrinterHub**.
> Ngày lập: 09/10/2026 · Các mục "⚠ cần xác minh" chưa được kiểm chứng trực tiếp.

---

## 1. Kết quả kiểm tra sơ bộ file (đã làm)

| Hạng mục | Kết quả |
|---|---|
| Định dạng vỏ | **ZIP** (chữ ký `PK 03 04`) |
| Mã hoá | **AES-256** (ZIP AES, vendor ver. 1, strength 3), nén Deflate bên trong |
| Có mật khẩu? | **Có** — mọi mục trong ZIP đều mã hoá; không giải nén được bằng 7-Zip/WinRAR thông thường |
| Nội dung (tên mục) | `Formats/50556_V9-000190-00V_` — 51 523 byte (khi giải nén) · `50556(V9-000190-00V).slnx` — 70 746 byte |
| Phiên bản tạo file | Định dạng `.nlbl` thuộc NiceLabel **2017 / 2019 / 10** (NiceLabel 6 trở về trước dùng `.lbl`) |

**Kết luận:** dữ liệu nhãn (gần như chắc là XML) nằm trong ZIP mã hoá do NiceLabel tạo ra. **Không có định dạng công khai và không có mật khẩu công khai** → cách đúng là đọc file **bằng phần mềm/SDK chính thức của NiceLabel**, rồi xuất ra dạng đọc được (XML, ảnh, PDF, file lệnh máy in).

> ⚠ Không nên tìm cách dò/bẻ mật khẩu ZIP: vừa vi phạm điều khoản bản quyền của nhà sản xuất, vừa không ổn định (cấu trúc XML bên trong có thể đổi theo phiên bản).

---

## 2. Trả lời nhanh các câu hỏi

### 2.1 NiceLabel có miễn phí không?

**Không.** NiceLabel (nay là **Loftware NiceLabel**) là phần mềm trả phí:

| Sản phẩm | Hình thức | Giá tham khảo (đại lý Mỹ) |
|---|---|---|
| Designer **Express** | bản quyền vĩnh viễn | ~ 355 USD |
| Designer **Pro** | bản quyền vĩnh viễn | ~ 595 USD |
| Designer **PowerForms** / **Automation** / **LMS** | bản quyền doanh nghiệp | liên hệ đại lý |
| **Loftware Cloud Designer** | thuê bao năm (1 máy in) | ~ 390 USD/năm |

Cách dùng **miễn phí hợp lệ**:
- **Dùng thử 30 ngày** bản Desktop (NiceLabel 10): khi cài đặt chọn chế độ trial; trong 30 ngày có thể chuyển qua lại giữa Designer Express / Pro / PowerForms.
- **Dùng thử 14 ngày** Loftware Cloud Designer.
- Nhờ **người/công ty đã gửi file** (có bản quyền) xuất giúp sang PDF / file lệnh / danh sách biến.

### 2.2 Phiên bản mới nhất & nơi tải

- Phiên bản desktop mới nhất được Loftware công bố: **NiceLabel 10.5** (trang tải cho khách hàng hiện tại cũng có 2019.3, 2017.3.1, 6.5.1…).
- Tải về:
  - Khách hàng có bản quyền: <https://loftware.com/customer-center/downloads/nicelabel-download-for-existing-customers> (điền email doanh nghiệp, chọn edition → nhận link `NiceLabel10.exe`).
  - Dùng thử: trang trial của Loftware (cần đăng ký; trang trial hiện chủ yếu là Loftware Cloud) — hoặc liên hệ **đại lý NiceLabel tại Việt Nam** để nhận bộ cài desktop trial ⚠ cần xác minh.
- Tài liệu: <https://help.nicelabel.com>

### 2.3 Có SDK / thư viện để đọc `.nlbl` không?

| Thư viện | Đọc `.nlbl` | Ghi chú |
|---|---|---|
| **NiceLabel .NET API** (`SDK.NET.Interface.dll`, thư mục `C:\Program Files\NiceLabel\NiceLabel 10\bin.net\`) | ✅ (`OpenLabel`) | Chính thức. Có trong edition **PowerForms Suite** và **LMS**; **NiceLabel SDK** bản độc lập dành cho đối tác Developer. Lấy được: danh sách **biến**, preview ảnh (`GetLabelPreview`), ảnh tất cả nhãn (`PrintToGraphics`), in, `SaveAs` (đổi định dạng) |
| **Automation** (action *Get Label Information*) | ✅ | Trả về **XML** mô tả nhãn (kích thước, máy in, biến…) |
| Loftware Cloud / Web API | ✅ (khi file nằm trong Document Storage) | Thuê bao cloud ⚠ |
| Thư viện mã nguồn mở / bên thứ ba | ❌ | Không tìm thấy thư viện nào đọc được `.nlbl` (file mã hoá, định dạng đóng) |
| 7-Zip, `System.IO.Compression` | ❌ | Đọc được danh sách mục, **không** giải mã được nội dung |

⚠ .NET API của NiceLabel 10 nhiều khả năng build cho **.NET Framework 4.x** → không nạp trực tiếp vào PrinterHub (.NET 10); cần một **tiến trình phụ .NET Framework 4.8** (xem mục 5).

### 2.4 NiceLabel có xuất sang dạng text (JSON/XML…) không?

| Cách xuất | Có? | Kết quả |
|---|---|---|
| Designer: xuất trực tiếp **JSON/XML** của toàn bộ thiết kế | ❌ không có menu xuất ra XML/JSON ⚠ cần xác minh | — |
| Automation: **Get Label Information** | ✅ | XML: kích thước nhãn, máy in, danh sách biến (tên, giá trị mặc định, độ dài, prompt…) |
| .NET API: `ILabel.Variables` + code tự viết | ✅ | Tự sinh JSON/XML theo ý muốn |
| **Print to file** (in ra file) | ✅ | File lệnh gốc của máy in (**SBPL / ZPL / EZPL**…) dạng text — đọc được và **gửi lại bằng PrinterHub** |
| In ra PDF (Microsoft Print to PDF) / preview PNG | ✅ | Ảnh/PDF để xem bố cục |
| `SaveAs` / Save As phiên bản cũ | ✅ | Vẫn là định dạng NiceLabel, không phải text |
| Command file (XML `<nice_commands>`, JOB, CSV) | ✅ ngược chiều | Dùng để **ra lệnh in** nhãn với biến, không phải đọc nhãn |

---

## 3. Tất cả các cách đọc file `.nlbl`

| # | Cách | Cần gì | Lấy được gì | Đánh giá |
|---|---|---|---|---|
| A | Mở bằng **NiceLabel Designer** (trial 30 ngày hoặc có license) | Windows, bộ cài NiceLabel 10 | Xem toàn bộ thiết kế, thuộc tính từng đối tượng, biến, nguồn dữ liệu, máy in | ⭐ nhanh nhất, thủ công |
| B | Designer → **Print to file** với driver máy in đích (SATO/Zebra/Godex) | Như A + driver NiceLabel cho máy in | File `.prn` chứa lệnh SBPL/ZPL/EZPL | ⭐ dùng ngay với PrinterHub |
| C | Designer → in ra **PDF** / chụp preview | Như A | Bố cục chính xác để vẽ lại | hỗ trợ |
| D | **Automation** → action *Get Label Information* | NiceLabel Automation (trial/license) | **XML** cấu trúc nhãn + biến | ⭐ tự động, có XML |
| E | **.NET API** (`OpenLabel`, `Variables`, `GetLabelPreview`, `PrintToGraphics`, in ra file) | PowerForms/LMS hoặc SDK; tiến trình .NET Framework | JSON do mình định nghĩa + PNG + `.prn` | ⭐ tự động hoá, tích hợp PrinterHub |
| F | **Command file** XML/JOB/CSV cho Automation: `LABEL` + `SET` biến + `PORT` ra file + `PRINT` | Automation | In nhãn với dữ liệu mới, xuất `.prn` | in hàng loạt |
| G | **Loftware Cloud** (trial 14 ngày): tải file lên Document Storage, mở bằng Cloud/Web Designer | Tài khoản cloud | Như A | khi không có Windows để cài |
| H | **Nhờ người gửi file** xuất: PDF, danh sách biến, `.prn` cho từng máy in, hoặc bản NiceLabel cũ | Liên lạc | Tuỳ yêu cầu | ⭐ rẻ nhất |
| I | Phân tích file thô (ZIP) | Python/C# | Chỉ được **tên mục, kích thước, ngày** (đã làm ở mục 1) | ❌ không đọc được nội dung |
| J | Bẻ mật khẩu / dò khoá | — | — | ❌ **không thực hiện** |

---

## 4. Kế hoạch thực hiện

### Giai đoạn 0 — Chuẩn bị (0.5 ngày)
- [ ] Hỏi người gửi file: phiên bản NiceLabel đã dùng, máy in đích (SATO? Zebra?), file dữ liệu/nguồn biến (Excel/CSV/DB) đi kèm, có thể gửi PDF + `.prn` không.
- [ ] Chuẩn bị một **máy Windows riêng / máy ảo** để cài bản dùng thử (tránh ảnh hưởng máy làm việc; bản trial 30 ngày).
- [ ] Sao lưu file `.nlbl` gốc (chỉ mở bản sao).

### Giai đoạn 1 — Mở và ghi nhận thủ công (1 ngày) — Cách A, B, C
1. Tải **NiceLabel 10.5** (trial) → cài **Desktop Designer** → chọn trial **Designer Pro** (hoặc PowerForms nếu nhãn dùng form/nguồn dữ liệu).
2. Mở `50556(V9-000190-00V).nlbl`, ghi lại vào `docs\nlbl\50556.md`:
   - Kích thước nhãn, khe hở, hướng, DPI, **máy in & driver** được gắn.
   - Từng đối tượng: loại (Text / Barcode / QR / Hình / Đường / Khung), vị trí X/Y, kích thước, font, xoay, nguồn dữ liệu (cố định / biến / công thức / CSDL).
   - Danh sách **biến**: tên, kiểu, độ dài, giá trị mặc định, prompt, bộ đếm.
   - Nguồn dữ liệu ngoài (Excel, CSV, SQL) nếu có.
3. Xuất:
   - **PDF** (Microsoft Print to PDF) và ảnh preview → `docs\nlbl\50556.pdf/png`.
   - **Print to file** bằng driver NiceLabel của **từng máy in đích** (SATO CL4NX Plus, Zebra ZD421, Godex G500) → `docs\nlbl\50556_sato.prn`, `…_zebra.prn`, `…_godex.prn`.
4. Gửi thử bằng PrinterHub:
   ```powershell
   printerhub send --route "sato4://192.168.1.50" --file docs\nlbl\50556_sato.prn
   printerhub send --route tcp://192.168.1.51:9100  --file docs\nlbl\50556_zebra.prn
   ```
   Nếu nhãn có biến: mở `.prn` (dạng text) trong tab **Lệnh raw** của PrinterHub để thấy chỗ chứa dữ liệu và thay giá trị.

### Giai đoạn 2 — Xuất XML tự động (1 ngày) — Cách D
1. Trong trial, mở **Automation Builder** → tạo cấu hình:
   - Trigger: *File trigger* theo dõi thư mục `C:\NL\in`.
   - Action: **Get Label Information** (file nhãn = file nhận được) → **Save Data to File** `C:\NL\out\<tên>.xml`.
2. Thả file `.nlbl` vào `C:\NL\in` → nhận XML. Ví dụ cấu trúc kỳ vọng ⚠ cần xác minh với output thật:
   ```xml
   <Label>
     <Original>
       <Width>100</Width><Height>50</Height><PrinterName>SATO CL4NX Plus 305dpi</PrinterName>
     </Original>
     <Variables>
       <Variable>
         <Name>PartNo</Name><DefaultValue>V9-000190-00V</DefaultValue>
         <Length>20</Length><IsPrompted>true</IsPrompted><PromptText>Mã hàng</PromptText>
       </Variable>
     </Variables>
   </Label>
   ```
3. Lưu XML vào `docs\nlbl\50556.labelinfo.xml`.

> XML này **chỉ mô tả biến và thông số nhãn**, không chứa đầy đủ toạ độ từng đối tượng → bố cục vẫn lấy từ PDF/preview hoặc `.prn`.

### Giai đoạn 3 — Công cụ `NlblInspector` bằng .NET API (2–3 ngày) — Cách E
Chỉ làm khi có bản quyền **PowerForms/LMS** hoặc **NiceLabel SDK** (hoặc trial cho phép dùng API ⚠ cần xác minh).

- Dự án mới `tools\NlblInspector` — **.NET Framework 4.8**, console, tham chiếu `SDK.NET.Interface.dll`.
- Chức năng:
  ```
  NlblInspector.exe info    <file.nlbl>                → JSON: kích thước, máy in, biến
  NlblInspector.exe preview <file.nlbl> <out.png>      → ảnh xem trước
  NlblInspector.exe prn     <file.nlbl> <printer> <out.prn> [--set Var=Val ...]  → in ra file lệnh
  ```
- Phác thảo code ⚠ tên thuộc tính cần kiểm tra trong *.NET API User Guide*:
  ```csharp
  PrintEngineFactory.SDKFilesPath = @"C:\Program Files\NiceLabel\NiceLabel 10\bin.net";
  var engine = PrintEngineFactory.PrintEngine;
  engine.Initialize();
  try
  {
      ILabel label = engine.OpenLabel(path);
      var info = new
      {
          file = path,
          printer = label.PrintSettings.PrinterName,
          variables = label.Variables.Select(v => new { v.Name, v.Value }).ToList(),
      };
      Console.WriteLine(JsonConvert.SerializeObject(info, Formatting.Indented));

      // Ảnh xem trước
      var ps = new LabelPreviewSettings { ImageFormat = "PNG", Width = 1000, Height = 600 };
      File.WriteAllBytes(outPng, (byte[])label.GetLabelPreview(ps));

      // In ra file .prn (lệnh SBPL/ZPL...) để PrinterHub gửi
      label.PrintSettings.PrinterName = printer;
      label.PrintSettings.PrintToFiles = true;          // ⚠
      label.PrintSettings.PrintToFileName = outPrn;     // ⚠
      label.Variables["PartNo"].SetValue("V9-000190-00V");
      label.Print(1);
  }
  finally { engine.Shutdown(); }
  ```
- Tích hợp PrinterHub: thêm lệnh CLI `printerhub nlbl info|prn …` gọi `NlblInspector.exe` (tiến trình phụ), đọc JSON trả về.

### Giai đoạn 4 — Chuyển sang nhãn PrinterHub (1–2 ngày)
1. Dựa vào PDF/preview + danh sách biến, dựng lại `samples\50556.label.json` (toạ độ dot, đúng DPI máy in đích), biến dùng cú pháp `{TênBiến}`.
2. So sánh bản in PrinterHub với bản in NiceLabel (đặt chồng lên nhau / so preview).
3. Tuỳ chọn: viết bộ chuyển `.prn` → JSON cho các lệnh đơn giản (text, barcode, box) — chỉ khi cần chuyển nhiều nhãn.

### Giai đoạn 5 — Quyết định lâu dài
| Phương án | Khi nào chọn |
|---|---|
| Giữ NiceLabel (mua license) + PrinterHub gửi `.prn`/command file | Nhiều nhãn `.nlbl`, khách hàng tiếp tục gửi file NiceLabel |
| Chuyển hẳn sang JSON nhãn PrinterHub | Ít nhãn, muốn bỏ phụ thuộc phần mềm trả phí |
| Mua NiceLabel SDK / PowerForms để đọc tự động | Cần đọc `.nlbl` thường xuyên trong hệ thống |

---

## 5. Rủi ro & lưu ý

| Rủi ro | Giảm thiểu |
|---|---|
| Trial chỉ 30 ngày | Làm Giai đoạn 1–2 liền nhau, xuất hết PDF/`.prn`/XML trong thời gian trial |
| Nhãn dùng font không có trên máy | Cài font theo cảnh báo của Designer; khi in `.prn` font có thể bị đổi thành ảnh |
| Nhãn lấy dữ liệu từ CSDL/Excel không có kèm | Xin file dữ liệu hoặc nhập giá trị mẫu |
| File tạo bằng phiên bản mới hơn bản đang cài | Dùng NiceLabel **10.5** (mới nhất) |
| .NET API không chạy trên .NET 10 | Tiến trình phụ .NET Framework 4.8 |
| Điều khoản bản quyền | Chỉ dùng phần mềm/SDK chính thức; không giải mã file thủ công |

---

## 6. Việc cần anh quyết định

1. Có thể **liên hệ người gửi file** để xin PDF / `.prn` / danh sách biến không? (nhanh và miễn phí nhất)
2. Có máy Windows/máy ảo để cài **NiceLabel 10.5 trial** không?
3. Công ty có (hoặc dự định mua) license **PowerForms / LMS / SDK** không? → quyết định có làm Giai đoạn 3.
4. Nhãn này sẽ in trên máy nào (SATO CL4NX Plus / Zebra ZD421 / Godex G500)?

---

## 7. Nguồn tham khảo

- Loftware – NiceLabel download for existing customers (phiên bản mới nhất 10.5): <https://loftware.com/customer-center/downloads/nicelabel-download-for-existing-customers>
- Choosing NiceLabel Trial Editions (trial 30 ngày): <https://help.nicelabel.com/hc/en-001/articles/4405121481105-Choosing-NiceLabel-Trial-Editions>
- Activating NiceLabel products: <https://help.nicelabel.com/hc/en-001/articles/23362175026577-Activating-NiceLabel-products-and-applications>
- Using NiceLabel .NET API: <https://help.nicelabel.com/hc/en-001/articles/10974888528145-Using-NiceLabel-NET-API>
- Programmable Integration (.NET API trong PowerForms/LMS, NiceLabel SDK, SaveAs): <https://help.nicelabel.com/hc/en-001/articles/10989437474961-Programmable-Integration>
- .NET API User Guide (PDF): <https://www.dcp.nl/media/wysiwyg/pdf-files/_NET_API_User_Guide-en.pdf>
- Command Files Specifications (XML/JOB/CSV): <https://help.nicelabel.com/hc/articles/360020970957>
- XML Data (Automation): <https://help.nicelabel.com/hc/articles/360020970937>
- Get Label Information (Loftware help): <https://help.loftware.com/cloud/Designer/Solutions/Define-Actions/Available-Actions/Other/Get-Label-Information.html>
- Giá tham khảo & trial cloud 14 ngày: <https://www.omegabrand.com/loftware-nicelabel-trial-demo-barcode-label-design-software-free-download/>
- Thông tin định dạng NLBL: <https://filext.com/file-extension/NLBL>
