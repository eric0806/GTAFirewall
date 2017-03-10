using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Media;
using System.Configuration;
using System.IO;

namespace GTAFirewall {
    public partial class MainForm : Form {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        enum KeyModifier {
            None = 0,
            Alt = 1,
            Control = 2,
            Shift = 4,
            WinKey = 8
        }

        string gtaFullPath;
        const string RULE_NAME = "GTA 5 單人公開戰局用";

        public MainForm() {
            InitializeComponent();

            //初始化GTA路徑
            InitPath();

            //註冊Hotkey
            int id = 0;
            RegisterHotKey(this.Handle, id, (int)KeyModifier.Control, (int)Keys.F12.GetHashCode());
            RegisterHotKey(this.Handle, 1, (int)KeyModifier.Control, (int)Keys.F1.GetHashCode());
            RegisterHotKey(this.Handle, 2, (int)KeyModifier.None, (int)Keys.Pause.GetHashCode());
        }

        void InitPath() {
            var path = ConfigurationManager.AppSettings["GTAPath"];
            var name = ConfigurationManager.AppSettings["GTAExe"];
            gtaFullPath = Path.Combine(path, name);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            /* 
            Unregister hotkey with id 0 before closing the form. 
            You might want to call this more than once with different id values 
            if you are planning to register more than one hotkey. 
            */
            UnregisterHotKey(this.Handle, 0);
            UnregisterHotKey(this.Handle, 1);
            UnregisterHotKey(this.Handle, 2);
        }

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);

            if (m.Msg == 0x0312) {
                /* Note that the three lines below are not needed if you only want to register one hotkey.
                 * The below lines are useful in case you want to register multiple keys, which you can use a switch with the id as argument, or if you want to know which key/modifier was pressed for some particular reason. */

                Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);                  // The key of the hotkey that was pressed.
                KeyModifier modifier = (KeyModifier)((int)m.LParam & 0xFFFF);       // The modifier of the hotkey that was pressed.
                int id = m.WParam.ToInt32();                                        // The id of the hotkey that was pressed.

#if DEBUG
                //MessageBox.Show("Hotkey has been pressed!");
#endif
                // do something
            }
        }

        /// <summary>
        /// 檢查防火牆狀態
        /// </summary>
        void CheckFirewall() {
            var arg = $"advfirewall firewall show rule name={RULE_NAME} | findstr /C:{RULE_NAME}";
            var proc = new ProcessStartInfo("netsh", arg);
            proc.RedirectStandardOutput = true;

        }

        /// <summary>
        /// 播放系統資源聲音
        /// </summary>
        /// <param name="isEnableSound"></param>
        private void PlaySound(bool isEnableSound) {
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
