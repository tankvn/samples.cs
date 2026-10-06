using PrinterHub.Core.Util;

namespace PrinterHub.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Giống dự án cp932: đăng ký CodePages để dùng Shift-JIS (932) cho lệnh Kanji SATO
        TextEncodings.EnsureRegistered();

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "PrinterHub — lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Application.Run(new MainForm());
    }
}
