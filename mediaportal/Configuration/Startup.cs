#region Copyright (C) 2005-2006 Team MediaPortal

/* 
 *	Copyright (C) 2005-2006 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

using System;
using System.Windows.Forms;
using MediaPortal.GUI.Library;
using MediaPortal.Util;
using System.Reflection;
using System.IO;

namespace MediaPortal.Configuration
{
  /// <summary>
  /// Summary description for Startup.
  /// </summary>
  public class Startup
  {
    enum StartupMode
    {
      Normal,
      Wizard
    }
    StartupMode startupMode = StartupMode.Normal;

    string sectionsConfiguration = String.Empty;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="arguments"></param>
    public Startup(string[] arguments)
    {
      if (!Config.LoadDirs(Application.StartupPath))
      {
        MessageBox.Show("Missing or Invalid MediaPortalDirs.xml file. MediaPortal cannot run without that file.", "MediaPortal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      // Logger should write into Configuration.log
      Log.SetConfigurationMode();
      Log.BackupLogFile(Log.LogType.Config);
      Log.Info("Using Directories:");
      foreach (string options in Enum.GetNames(typeof(Config.Dir)))
      {
        Log.Info("{0} - {1}", options, Config.Get((Config.Dir)Enum.Parse(typeof(Config.Dir), options)));
      }
      if (!System.IO.File.Exists(Config.Get(Config.Dir.Config) + "mediaportal.xml"))
        startupMode = StartupMode.Wizard;

      else if (arguments != null)
      {
        foreach (string argument in arguments)
        {
          string trimmedArgument = argument.ToLower();

          if (trimmedArgument.StartsWith("/wizard"))
          {
            startupMode = StartupMode.Wizard;
          }

          if (trimmedArgument.StartsWith("/section"))
          {
            string[] subArguments = argument.Split('=');

            if (subArguments.Length >= 2)
            {
              sectionsConfiguration = subArguments[1];
            }
          }
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public void Start()
    {
      Log.Info("Configuration is starting up");

      FileInfo mpFi = new FileInfo(Assembly.GetExecutingAssembly().Location);
      Log.Info("Assembly creation time: {0} (UTC)", mpFi.LastWriteTimeUtc.ToUniversalTime());

      Form applicationForm = null;

      Thumbs.CreateFolders();

      switch (startupMode)
      {
        case StartupMode.Normal:
          Log.Info("Create new standard setup");
          applicationForm = new SettingsForm();
          break;

        case StartupMode.Wizard:
          Log.Info("Create new wizard setup");
          applicationForm = new WizardForm(sectionsConfiguration);
          break;
      }


      if (applicationForm != null)
      {

        Log.Info("start application");
        System.Windows.Forms.Application.Run(applicationForm);
      }
    }

    [STAThread]
    public static void Main(string[] arguments)
    {
      try
      {

        

        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.DoEvents();

        new Startup(arguments).Start();
      }
      finally
      {
        GC.Collect();
      }
    }

    private static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
      if (args.Name.Contains(".resources"))
        return null;
      if (args.Name.Contains(".XmlSerializers"))
        return null;
      MessageBox.Show("Failed to locate assembly '" + args.Name + "'." + Environment.NewLine + "Note that the configuration program must be executed from/reside in the MediaPortal folder, the execution will now end.", "MediaPortal", MessageBoxButtons.OK, MessageBoxIcon.Error);
      System.Windows.Forms.Application.Exit();
      return null;
    }
  }
}
