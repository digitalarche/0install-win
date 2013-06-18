﻿/*
 * Copyright 2010-2013 Bastian Eicher
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Common;
using Common.Cli;
using Common.Controls;

namespace ZeroInstall.Publish.WinForms
{
    /// <summary>
    /// Launches a WinForms-based editor for Zero Install feed XMLs.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ErrorReportForm.SetupMonitoring(new Uri("http://0install.de/error-report/"));

            if (args.Length == 0) Application.Run(new MainForm(new FeedEditing()));
            else
            {
                var files = ArgumentUtils.GetFiles(args, "*.xml");
                if (files.Count == 1)
                {
                    string path = files.First().FullName;
                    try
                    {
                        Application.Run(new MainForm(FeedEditing.Load(path)));
                    }
                        #region Error handling
                    catch (IOException ex)
                    {
                        Msg.Inform(null, ex.Message, MsgSeverity.Error);
                    }
                    #endregion
                }
                else MassSignForm.Show(files);
            }
        }
    }
}
