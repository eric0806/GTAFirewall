using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GTAFirewall {
    public partial class AboutForm : Form {
        public AboutForm() {
            InitializeComponent();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            try {
                GoLink();
            }
            catch {
                MessageBox.Show("嗚...沒辦法開啟連結QQ", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void GoLink() {
            System.Diagnostics.Process.Start("https://github.com/eric0806/GTAFirewall");
        }
    }
}
