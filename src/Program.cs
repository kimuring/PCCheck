using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// exe のプロパティ（右クリック → 詳細）に表示される情報 / exe 속성(오른쪽 클릭 → 자세히)에 표시되는 정보
[assembly: AssemblyTitle("PC Check")]
[assembly: AssemblyDescription("PC status check tool (hardware / security / resource usage)")]
[assembly: AssemblyProduct("PC Check")]
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]

namespace PCCheck
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Settings.Load();   // 前回選んだ言語を読み込む / 지난번에 고른 언어 불러오기

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 予期しないエラーで黙って落ちないように / 예상치 못한 오류로 조용히 꺼지지 않게
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

            Application.Run(new MainForm());
        }

        private static void ShowError(Exception ex)
        {
            MessageBox.Show(L.F("msg.error", ex == null ? "?" : ex.Message), L.T("app.name"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
