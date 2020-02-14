using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Manina.Windows.Forms;

namespace RTCV.Plugins.ScriptHost.Controls
{
    sealed class ScriptManagerTab : Tab
    {
        public ScriptManager ScriptManager;
        private Color DarkerGray = Color.FromArgb(64, 64, 64);

        public ScriptManagerTab(string name = null)
        {
            ScriptManager = new ScriptManager();

            Name = name ?? "New Script";
            Text = name ?? "New Script";
            BackColor = DarkerGray;
            ForeColor = Color.White;
        }
    }
}
