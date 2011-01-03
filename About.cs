using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace AirStreamPlayer
{
    public partial class About : Form
    {
        public About()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Launches default browser to go to www.tomthorpe.com
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bylink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.tomthorpe.com");
        }
    }
}