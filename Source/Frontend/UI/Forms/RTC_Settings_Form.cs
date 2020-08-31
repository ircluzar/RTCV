﻿namespace RTCV.UI
{
    using System;
    using System.Diagnostics;
    using System.Windows.Forms;
    using RTCV.CorruptCore;
    using RTCV.Common;
    using RTCV.UI.Modular;

    #pragma warning disable CA2213 //Component designer classes generate their own Dispose method
    public partial class RTC_Settings_Form : ComponentForm, IAutoColorize, IBlockable
    {
        public new void HandleMouseDown(object s, MouseEventArgs e) => base.HandleMouseDown(s, e);
        public new void HandleFormClosing(object s, FormClosingEventArgs e) => base.HandleFormClosing(s, e);

        public RTC_ListBox_Form lbForm;

        public RTC_Settings_Form()
        {
            InitializeComponent();

            lbForm = new RTC_ListBox_Form(new ComponentForm[] {
                S.GET<RTC_SettingsGeneral_Form>(),
                S.GET<RTC_SettingsCorrupt_Form>(),
                S.GET<RTC_SettingsHotkeyConfig_Form>(),
                S.GET<RTC_SettingsNetCore_Form>(),
                S.GET<RTC_SettingsAbout_Form>(),
            })
            {
                popoutAllowed = false
            };

            lbForm.AnchorToPanel(pnListBoxForm);
        }

        private void btnRtcFactoryClean_Click(object sender, EventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = "FactoryClean.bat";
            p.StartInfo.WorkingDirectory = RtcCore.EmuDir;
            p.Start();
        }

        private void RTC_Settings_Form_Load(object sender, EventArgs e)
        {
            if (Debugger.IsAttached)
            {
                btnTestForm.Show();
            }
        }

        private void btnToggleConsole_Click(object sender, EventArgs e)
        {
            LogConsole.ToggleConsole();
        }

        private void btnDebugInfo_Click(object sender, EventArgs e)
        {
            S.GET<NetCore.DebugInfo_Form>().ShowDialog();
        }

        private void BtnTestForm_Click(object sender, EventArgs e)
        {
            var testform = new RTC_Test_Form();
            testform.Show();
        }
    }
}
