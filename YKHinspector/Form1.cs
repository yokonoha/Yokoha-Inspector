using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YKHinspector
{

    public partial class Form1 : Form
    {
        Point lastPoint;// i tsu mo no
        private KeyValuePair<string, string>[] batKV = new KeyValuePair<string, string>[0];
        private int kansouflag = 1;
        private int valueERR = 0;
        //翻訳用
        private string normalERR = "エラーが発生しました。";
        private string transERR1 = "バッテリー情報のデータを取得できませんでした。";
        private string transttl1 = "のバッテリー状況";
        private string transNotice1="バッテリーが搭載されていない端末です。\nバッテリー情報は利用できません。";
        private string transNotice2 = "取得したバッテリー情報が不正値のため、評価できませんでした。\n設計容量値が0と報告されました。バッテリーに何らかの問題がある可能性があります。";
        private string transERR2="満充電容量が設計容量を上回っています。\nバッテリーの劣化率算出は正しく行えません。";
        private string transERR3="不正確";
        private string transERR4="バッテリー情報の校正が必要です。\n(満充電・放電を数回繰り返し行って下さい。)\n現時点では、バッテリーの劣化率算出は正しく行えません。\n詳細は「その他情報」を参照してください。";
        private string transERR5="バッテリー劣化率の算出に失敗しました。";
        private string transERR6 = "バッテリー情報の解析に失敗しました。";
        private string transERR7 = "解析に必要なデータが見つかりませんでした。";
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            getbattstatus();
            if(kansouflag == 0)
            {
                MessageBox.Show(transNotice1, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                label2.Text = "N/A";
                label3.Text = "Z";
                label8.Text = "N/A";
                label9.Text = "N/A";
                label11.Text = "N/A";
                label22.ForeColor = Color.Red;
            }
            else
            {
                processrawdata();
            }
               
        }

        private void getbattstatus()
        {
            string tmpdir = Path.Combine(Path.GetTempPath(), "YKHinspector" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tmpdir);
                string rptfile = Path.Combine(tmpdir, "battery-report.html");

                ProcessStartInfo getdata = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = "/batteryreport /output \"" + rptfile + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process prc = new Process())
                {
                    prc.StartInfo = getdata;
                    prc.Start();
                    string optmsg = prc.StandardOutput.ReadToEnd();
                    string errormsg = prc.StandardError.ReadToEnd();
                    prc.WaitForExit();
                    if (prc.ExitCode != 0)
                    {
                        //A Trans
                        throw new Exception(transERR1 + prc.ExitCode + " - " + errormsg + " - " + optmsg);
                    }
                }
                /////////////////PARSE///////////////////////////////////
                string html = File.ReadAllText(rptfile, Encoding.UTF8);
                if(html.Contains("No batteries are currently installed"))
                {
                  kansouflag = 0;
                }
                Console.WriteLine("DEBUG LENGTH:" + html.Length);

                //trタグ取得
                MatchCollection rows = Regex.Matches(html, @"<tr>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                Console.WriteLine("DEBUG ROWS:" + rows.Count);

                foreach (Match row in rows)
                {
                    Console.WriteLine("--------------------");
                    Console.WriteLine(row.Groups[1].Value);
                    //tdタグ取得
                    MatchCollection vinfo = Regex.Matches(row.Groups[1].Value, @"<td\b[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    Console.WriteLine("DEBUG VINFO:" + vinfo.Count);
                    try
                    {
                        Console.WriteLine("DEBUG RES:" + vinfo[0].Groups[1].Value);
                        Console.WriteLine("DEBUG RES:" + vinfo[1].Groups[1].Value);
                        string ttl = Regex.Replace(vinfo[0].Groups[1].Value, @"<[^>]+>", "").Trim();
                        string val = Regex.Replace(vinfo[1].Groups[1].Value, @"<[^>]+>", "").Trim();
                        Array.Resize(ref batKV, batKV.Length + 1);
                        batKV[batKV.Length - 1] = new KeyValuePair<string, string>(ttl, val);
                        

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("DEBUG ERROR:" + ex.Message);
                        //KVがそろわない。
                    }

                    if (kansouflag == 1)
                    {
                        Console.WriteLine(string.Join(", ", batKV.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
                    }

                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, normalERR, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try
                {
                    Directory.Delete(tmpdir, true);
                }
                catch { }
            }
        }
        private void processrawdata()
        {
            //res
            var fcc = batKV.FirstOrDefault(x => x.Key == "FULL CHARGE CAPACITY");
            var dcc = batKV.FirstOrDefault(x => x.Key == "DESIGN CAPACITY");
            
            //返却想定値 53,270mWh
            if (fcc.Key != null && dcc.Key != null)
            {
                string fccval = Regex.Replace(fcc.Value, @"[^\d]", "");
                string dccval = Regex.Replace(dcc.Value, @"[^\d]", "");
               
                if (int.TryParse(fccval, out int fccint) && int.TryParse(dccval, out int dccint))
                {
                    try
                    {
                        if (dccint == 0) { valueERR = 1; }
                        double ratio = (double)fccint / dccint;
                        Console.WriteLine($"FULL CHARGE CAPACITY: {fccint} mWh");
                        Console.WriteLine($"DESIGN CAPACITY: {dccint} mWh");
                        Console.WriteLine($"Capacity Ratio: {ratio:P1}");
                        //劣化率
                        label2.Text = $"{ratio:P1}";
                        //label9 DC
                        label9.Text = $"{dccint} mWh";
                        //label8 FCC
                        label8.Text = $"{fccint} mWh";
                        if (fccint > dccint)
                        {
                            label2.ForeColor = Color.Red;
                            valueERR = 1;
                            label3.ForeColor = Color.Red;
                            label9.ForeColor = Color.Red;
                            label8.ForeColor = Color.Red;
                            textBox1.AppendText(transERR2 + Environment.NewLine);
                            MessageBox.Show(transERR4, normalERR, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        if (dccint == 0)
                        {
                            MessageBox.Show(transNotice2, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            label2.Text = "N/A";
                            label3.Text = "N/A";
                            label8.Text = "N/A";
                            label9.Text = "N/A";
                            label11.Text = "N/A";
                            label23.ForeColor = Color.Red;
                        }
                        else
                        {
                            //レベルcalc A:95%以上, B:85~95%, C:80~85%, D:65~80%, E:70%~50%, F:50%未満 => dsp label3
                            if (ratio >= 0.95)
                            {
                                label3.Text = "A";
                                label3.ForeColor = Color.Green;
                                label2.ForeColor = Color.Green;
                                label16.ForeColor = Color.Green;
                            }
                            else if (ratio >= 0.85)
                            {
                                label3.Text = "B";
                                label3.ForeColor = Color.Blue;
                                label2.ForeColor = Color.Blue;
                                label17.ForeColor = Color.Blue;
                            }
                            else if (ratio >= 0.80)
                            {
                                label3.Text = "C";
                                label3.ForeColor = Color.Orange;
                                label2.ForeColor = Color.Orange;
                                label18.ForeColor = Color.Orange;
                            }
                            else if (ratio >= 0.65)
                            {
                                label3.Text = "D";
                                label3.ForeColor = Color.OrangeRed;
                                label2.ForeColor = Color.OrangeRed;
                                label19.ForeColor = Color.OrangeRed;
                            }
                            else if (ratio >= 0.50)
                            {
                                label3.Text = "E";
                                label3.ForeColor = Color.Red;
                                label2.ForeColor = Color.Red;
                                label20.ForeColor = Color.Red;
                            }
                            else
                            {
                                label3.Text = "F";
                                label3.ForeColor = Color.DarkRed;
                                label2.ForeColor = Color.DarkRed;
                                label21.ForeColor = Color.DarkRed;
                            }
                            if (valueERR == 1)
                            {
                                label3.Text += " (" + transERR3 + ")";
                            }
                            retriveOptionalData();

                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error calculating capacity ratio: " + ex.Message);
                        textBox1.AppendText(transERR5 + Environment.NewLine);
                    }
                }
                else
                {
                    Console.WriteLine("Failed to parse capacity values.");
                    textBox1.AppendText(transERR6 + Environment.NewLine);
                }
            }
            else
            {
                Console.WriteLine("Required capacity information not found.");
                textBox1.AppendText (transERR7 + Environment.NewLine);
            }
        }
        private void retriveOptionalData()
        {
            //オプションデータ(飾りもの) PCモデル名, BIOSバージョン, OSビルド番号, 調査時刻,バッテリータイプ,バッテリーメーカー
            /*
                            var BIOS = batKV.FirstOrDefault(x => x.Key == "BIOS");
                            var OSBUILD = batKV.FirstOrDefault(x => x.Key == "OS BUILD");
                            var REPORTTIME = batKV.FirstOrDefault(x => x.Key == "REPORT TIME");
                            var BATTERYNAME = batKV.FirstOrDefault(x => x.Key == "NAME");
                            var CHEMISTRY = batKV.FirstOrDefault(x => x.Key == "CHEMISTRY");
                            var MANUFACTURER = batKV.FirstOrDefault(x => x.Key == "MANUFACTURER");
             */
            //一応1つずつ作業。
            try
            {
                var SPN = batKV.FirstOrDefault(x => x.Key == "SYSTEM PRODUCT NAME");
                label11.Text = SPN.Value+transttl1+"\nby Yokoha Inspector" ?? "N/A";

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving optional data: " + ex.Message);
            }

            try
            {
                var BIOS = batKV.FirstOrDefault(x => x.Key == "BIOS");
                textBox1.AppendText("BIOS: " + BIOS.Value + Environment.NewLine ?? "N/A" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving BIOS data: " + ex.Message);
            }

            try
            {
                var OSBUILD = batKV.FirstOrDefault(x => x.Key == "OS BUILD");
                textBox1.AppendText("OS BUILD: " + OSBUILD.Value + Environment.NewLine ?? "N/A" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving OS BUILD data: " + ex.Message);
            }

            try
            {
                var REPORTTIME = batKV.FirstOrDefault(x => x.Key == "REPORT TIME");
                textBox1.AppendText("REPORT TIME: " + REPORTTIME.Value + Environment.NewLine ?? "N/A" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving REPORT TIME data: " + ex.Message);
            }
            try
            {
                var BATTERYNAME = batKV.FirstOrDefault(x => x.Key == "NAME");
                textBox1.AppendText("BATTERY NAME: " + BATTERYNAME.Value + Environment.NewLine ?? "N/A" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving BATTERY NAME data: " + ex.Message);
            }

            try
            {
                var CHEMISTRY = batKV.FirstOrDefault(x => x.Key == "CHEMISTRY");
                textBox1.AppendText("CHEMISTRY: " + CHEMISTRY.Value + Environment.NewLine ?? "N/A" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving CHEMISTRY data: " + ex.Message);
            }
            try
            {
                var MANUFACTURER = batKV.FirstOrDefault(x => x.Key == "MANUFACTURER");
                textBox1.AppendText("MANUFACTURER: " + MANUFACTURER.Value + Environment.NewLine ?? "N/A" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving MANUFACTURER data: " + ex.Message);
            }

            }

        private void label5_Click(object sender, EventArgs e)
        {
        this.Close();
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            lastPoint = e.Location;
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - lastPoint.X;
                 this.Top += e.Y - lastPoint.Y;
            }
        }

        private void label11_MouseDown(object sender, MouseEventArgs e)
        {
            lastPoint = e.Location;
        }

        private void label11_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - lastPoint.X;
                this.Top += e.Y - lastPoint.Y;
            }
        }

        private void label13_Click(object sender, EventArgs e)
        {
            //yokonoha.github.ioへつれてく。
            System.Diagnostics.Process.Start("https://yokonoha.github.io/");
        }

        private void label15_Click(object sender, EventArgs e)
        {
            //利用規約へつれてく (yokonoha.github.io/readme)
            System.Diagnostics.Process.Start("https://yokonoha.github.io/readme");
        }
    }
}
