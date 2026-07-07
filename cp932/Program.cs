using System.Text;

namespace CsvReader;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Đăng ký CodePages để hỗ trợ Shift-JIS (CP932) và các encoding khác
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
