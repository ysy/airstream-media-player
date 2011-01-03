/*
   Copyright (C) 2011 Tom Thorpe

   This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 2 of the License, or (at your option) any later version.

   This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details. You should have received a copy of the GNU General Public License along with this program; if not, write to the Free Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AirStreamPlayer
{
    public partial class CheckboxMessageBox : Form
    {
        public CheckboxMessageBox(string message)
        {
            InitializeComponent();
            this.message.Text = message;
        }

        /// <summary>
        /// Shows the message box
        /// </summary>
        /// <returns>True if "OK" was clicked and the "don't show this message again" box was checked, false otherwise </returns>
        public bool showMessage()
        {
            DialogResult result = this.ShowDialog();
            if (result == DialogResult.OK)
            {
                return dontshowagain.Checked;
            }
            else
            {
                return false;
            }
        }
    }
}
