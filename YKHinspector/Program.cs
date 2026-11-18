using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YKHinspector
{
    internal static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MessageBox.Show("起動準備が整いました。\nバッテリーデータの取得には時間がかかることがあります。\n(OKを押すと取得を開始します。)", "Yokoha Inspector", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Run(new Form1());
        }
    }
}
