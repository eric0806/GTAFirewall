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
using Microsoft.Win32;

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
        /// 防火牆檢查狀態
        /// </summary>
        enum FirewallCheckStatus {
            NotExists = 0,
            Exists = 1,
            Error = 99
        }

        /// <summary>
        /// 在執行Process時是否發生錯誤
        /// </summary>
        bool cmdError = false;
        /// <summary>
        /// 執行Process時的錯誤訊息(如果發生錯誤的話)
        /// </summary>
        string cmdErrorMessage = string.Empty;
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
            //修改(Tast)：預先讀取註冊表
            if (!RegistryPath())
            {
                if (!InitPath())
                {
                    Environment.Exit(0);
                    return;
                }
            }

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
            //刪除防火牆規則，免得忘了開，下次連不上
            DelRules();
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
        /// 由系統註冊表取得遊戲執行檔路徑 by Tast
        /// 參考來源：https://stackoverflow.com/a/18234755
        /// 註冊表：https://forum.gamer.com.tw/Co.php?bsn=4737&sn=246515
        /// </summary>
        bool RegistryPath()
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Rockstar Games\\Grand Theft Auto V");
                if (key != null)
                {
                    Object o = key.GetValue("InstallFolder");
                    if (o != null)
                    {
                        gtaFullPath = o.ToString() + "\\GTA5.exe";
                        if (!File.Exists(gtaFullPath)) return false;
                        else
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)  //just for demonstration...it's always best to handle specific exceptions
            {
                Console.WriteLine(ex.Message.ToString());
                return false;
            }
        }

        /// <summary>
        /// 由設定檔讀取並產生完整的GTA5.exe執行檔路徑
        /// 修改(Tast)：預先從系統註冊表讀取路徑，無目標檔案則回歸設定檔模式
        /// </summary>
        bool InitPath() {
            var file = ".\\path.txt";
            if (!File.Exists(file)) {
                MessageBox.Show("您沒有path.txt!!\n請在程式相同目錄底下建立path.txt，\n並設定GTA的路徑!", "找不到檔案", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            try {
                gtaFullPath = File.ReadLines(file).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(gtaFullPath)) {
                    MessageBox.Show("您沒有設定GTA5.exe的路徑!\n請設定後再重試一次!", "檔案內容錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch {
                MessageBox.Show("開啟path.txt錯誤!", "開啟檔案錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!File.Exists(gtaFullPath)) {
                MessageBox.Show("找不到GTA5.exe!\n請檢查路徑設定!!", "找不到檔案", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 檢查防火牆狀態並自動設定
        /// <para>如果防火牆內無此規則，則建立兩個規則(in和out)來關閉連線；若有此規則，則刪除他們來恢復連線</para>
        /// </summary>
        void AutomatingFirewall() {
            switch (RuleExists()) {
                case FirewallCheckStatus.Exists: {
                        //如果規則存在，表示目前gta無法連網，要將之啟用(刪除規則)
                        var result = DelRules();
                        if (!result.Item1) {
                            MessageBox.Show(result.Item2, "發生錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            label_Status.Text = "狀態：規則刪除錯誤(無法准許連線)";
                            label_Status.ForeColor = System.Drawing.Color.DarkRed;
                        }
                        else {
                            PlaySound(true);
                            label_Status.Text = "狀態：規則刪除成功(已准許連線)";
                            label_Status.ForeColor = System.Drawing.Color.ForestGreen;
                        }
                        break;
                    }
                case FirewallCheckStatus.NotExists: {
                        //反之則表示gta可連網，要新增規則將他斷網
                        var result = AddRules();
                        if (!result.Item1) {
                            MessageBox.Show(result.Item2, "發生錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            label_Status.Text = "狀態：規則建立失敗(無法斷網)";
                            label_Status.ForeColor = System.Drawing.Color.DarkRed;
                        }
                        else {
                            PlaySound(false);
                            label_Status.Text = "狀態：規則建立成功(遊戲斷網)";
                            label_Status.ForeColor = System.Drawing.Color.ForestGreen;
                        }
                        break;
                    }
                case FirewallCheckStatus.Error: {
                        //發生錯誤
                        MessageBox.Show(cmdErrorMessage, "發生錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        label_Status.Text = "狀態：防火牆??";
                        label_Status.ForeColor = System.Drawing.Color.DarkRed;
                        break;
                    }
            }
        }

        /// <summary>
        /// 檢查規則是否已存在
        /// </summary>
        /// <returns></returns>
        FirewallCheckStatus RuleExists() {
            var arg = $"advfirewall firewall show rule name=\"{RULE_NAME}\"";
            var cmdResult = RunProcess("netsh", arg);
            if (cmdError) {
                return FirewallCheckStatus.Error;
            }
            else {
                var regex = new Regex(RULE_NAME, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                return regex.IsMatch(cmdResult) ? FirewallCheckStatus.Exists : FirewallCheckStatus.NotExists;
            }
        }

        /// <summary>
        /// 增加規則(GTA禁止連網)
        /// </summary>
        /// <returns></returns>
        Tuple<bool, string> AddRules() {
            DelRules();
            var arg1 = $"advfirewall firewall add rule name=\"{RULE_NAME}\" dir=in action=block program=\"{gtaFullPath}\" enable=yes";
            var result1 = RunProcess("netsh", arg1);
            if (cmdError) {
                return new Tuple<bool, string>(false, $"發生錯誤(1):{cmdErrorMessage}");
            }
            var arg2 = $"advfirewall firewall add rule name=\"{RULE_NAME}\" dir=out action=block program=\"{gtaFullPath}\" enable=yes";
            var result2 = RunProcess("netsh", arg2);
            if (cmdError) {
                return new Tuple<bool, string>(false, $"發生錯誤(2):{cmdErrorMessage}");
            }
            
            return new Tuple<bool, string>(true, "");
        }

        /// <summary>
        /// 刪除規則(GTA可以連網)
        /// </summary>
        /// <returns></returns>
        Tuple<bool, string> DelRules() {
            var arg = $"advfirewall firewall delete rule name=\"{RULE_NAME}\"";
            var result = RunProcess("netsh", arg);
            if (cmdError) {
                return new Tuple<bool, string>(false, $"發生錯誤(3):{cmdErrorMessage}");
            }
            
            return new Tuple<bool, string>(true, "");
        }

        /// <summary>
        /// 執行指令並回傳執行結果(文字)
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        string RunProcess(string cmd, string arg) {
            cmdError = false;
            cmdErrorMessage = string.Empty;
            var procInfo = new ProcessStartInfo(cmd, arg);
            procInfo.RedirectStandardOutput = true;
            procInfo.UseShellExecute = false;
            procInfo.CreateNoWindow = true;

            var process = new Process();
            process.StartInfo = procInfo;
            process.ErrorDataReceived += Process_ErrorDataReceived;
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEnd();
            //MessageBox.Show(standardOutput);
            return standardOutput;
        }

        /// <summary>
        /// 收到錯誤資料時的處理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            cmdError = true;
            cmdErrorMessage = e.Data;
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
