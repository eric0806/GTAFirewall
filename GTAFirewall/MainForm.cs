using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Diagnostics;
using System.Media;
using System.Configuration;
using System.IO;

namespace GTAFirewall {
    public partial class MainForm : Form {
        #region 引用user32.dll
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        #endregion

        #region 定義變數及常數
        /// <summary>
        /// 特殊控制鍵的碼
        /// </summary>
        enum KeyModifier {
            None = 0,
            Alt = 1,
            Control = 2,
            Shift = 4,
            WinKey = 8
        }

        /// <summary>
        /// GTA5.exe執行檔的完整路徑
        /// </summary>
        string gtaFullPath;
        /// <summary>
        /// 防火牆規則的名稱
        /// </summary>
        const string RULE_NAME = "GTA 5 單人公開戰局用";
        #endregion

        public MainForm() {
            InitializeComponent();

            //初始化GTA路徑
            InitPath();

            //註冊三個Hotkey
            int id = 0;
            RegisterHotKey(this.Handle, id, (int)KeyModifier.Control, (int)Keys.F12.GetHashCode()); //Ctrl+F12
            RegisterHotKey(this.Handle, 1, (int)KeyModifier.Control, (int)Keys.F1.GetHashCode());   //Ctrl+F1
            RegisterHotKey(this.Handle, 2, (int)KeyModifier.None, (int)Keys.Pause.GetHashCode());   //Pause
        }

        /// <summary>
        /// 程式關閉前的處理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            /* 
             * 程式關閉時我們要釋放已經註冊的Hotkey，以免造成問題
             */
            UnregisterHotKey(this.Handle, 0);
            UnregisterHotKey(this.Handle, 1);
            UnregisterHotKey(this.Handle, 2);
        }

        /// <summary>
        /// 按下Hotkey的處理
        /// </summary>
        /// <param name="m"></param>
        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            if (m.Msg == 0x0312) {
                Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);
                KeyModifier modifier = (KeyModifier)((int)m.LParam & 0xFFFF);
                int id = m.WParam.ToInt32();

#if DEBUG
                //MessageBox.Show("Hotkey has been pressed!");
#endif
                //按下Hotkey要做的工作就放在這
                AutomatingFirewall();
            }
        }

        /// <summary>
        /// 由設定檔讀取並產生完整的GTA5.exe執行檔路徑
        /// </summary>
        void InitPath() {
            var path = ConfigurationManager.AppSettings["GTAPath"];
            var name = ConfigurationManager.AppSettings["GTAExe"];
            gtaFullPath = Path.Combine(path, name);
        }


        /// <summary>
        /// 檢查防火牆狀態並自動設定
        /// <para>如果防火牆內無此規則，則建立兩個規則(in和out)來關閉連線；若有此規則，則刪除他們來恢復連線</para>
        /// </summary>
        void AutomatingFirewall() {
            //var arg = $"advfirewall firewall show rule name={RULE_NAME} | findstr /C:{RULE_NAME}";
            var arg = $"advfirewall firewall show rule name=\"{RULE_NAME}\"";
            var cmdResult = RunProcess("netsh", arg);
            var regex = new Regex(RULE_NAME, RegexOptions.Multiline);
            var matches = regex.Matches(cmdResult);
            MessageBox.Show(matches.Count.ToString());
        }



        /// <summary>
        /// 執行指令並回傳執行結果(文字)
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        string RunProcess(string cmd, string arg) {
            var proc = new ProcessStartInfo(cmd, arg);
            proc.RedirectStandardOutput = true;
            proc.UseShellExecute = false;
            proc.CreateNoWindow = true;

            return Process.Start(proc).StandardOutput.ReadToEnd();
        }

        /// <summary>
        /// 播放系統資源聲音
        /// </summary>
        /// <param name="isEnableSound"></param>
        void PlaySound(bool isEnableSound) {
            SoundPlayer player;
            if (isEnableSound) {
                player = new SoundPlayer(Properties.Resources.edEnable);
            }
            else {
                player = new SoundPlayer(Properties.Resources.edDisable);
            }
            player.Play();
        }

    }
}
