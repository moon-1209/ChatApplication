using ChatApplicationClient.UI;

namespace ChatApplicationClient
{
    internal static class Program
    {
        /// <summary>Điểm vào của ứng dụng client (WinForms).</summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize(); // bật DPI awareness + visual styles
            Application.Run(new ConnectForm());
        }
    }
}
