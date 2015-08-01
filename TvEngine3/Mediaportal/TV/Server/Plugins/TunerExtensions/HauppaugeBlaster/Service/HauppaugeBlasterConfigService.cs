﻿#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.IO;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using MediaPortal.Common.Utils;
using Microsoft.Win32;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.HauppaugeBlaster.Service
{
  internal class HauppaugeBlasterConfigService : IHauppaugeBlasterConfigService
  {
    public delegate void OnBlasterConfigChange(string tunerExternalIdPort1, string tunerExternalIdPort2);
    public event OnBlasterConfigChange OnConfigChange;

    private Blaster _blaster = new Blaster();

    ~HauppaugeBlasterConfigService()
    {
      _blaster.CloseInterface();
    }

    public void GetBlasterTunerExternalIds(out string tunerExternalIdPort1, out string tunerExternalIdPort2)
    {
      tunerExternalIdPort1 = SettingsManagement.GetValue("hauppaugeBlasterTunerPort1", string.Empty);
      tunerExternalIdPort2 = SettingsManagement.GetValue("hauppaugeBlasterTunerPort2", string.Empty);
    }

    public void SaveBlasterTunerExternalIds(string tunerExternalIdPort1, string tunerExternalIdPort2)
    {
      SettingsManagement.SaveValue("hauppaugeBlasterTunerPort1", tunerExternalIdPort1);
      SettingsManagement.SaveValue("hauppaugeBlasterTunerPort2", tunerExternalIdPort2);
      if (OnConfigChange != null)
      {
        OnConfigChange(tunerExternalIdPort1, tunerExternalIdPort2);
      }
    }

    public void GetBlasterInstallDetails(out string irBlastVersion, out string blastCfgLocation, out bool isHcwIrBlastDllPresent, out string blasterVersion, out int blasterPortCount)
    {
      irBlastVersion = null;
      blastCfgLocation = null;
      isHcwIrBlastDllPresent = false;
      blasterVersion = _blaster.GetVersion();
      blasterPortCount = _blaster.GetPortCount();

      using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hauppauge WinTV IR Blaster"))
      {
        if (key != null)
        {
          try
          {
            object value = key.GetValue("DisplayVersion");
            if (value != null)
            {
              irBlastVersion = value.ToString();
            }

            value = key.GetValue("InstallLocation");
            if (value != null)
            {
              blastCfgLocation = Path.Combine(value.ToString(), "BlastCfg.exe");
              if (!File.Exists(blastCfgLocation))
              {
                blastCfgLocation = null;
              }
            }
          }
          finally
          {
            key.Close();
          }
        }
      }

      try
      {
        IntPtr handle = NativeMethods.LoadLibrary("hcwIRblast.dll");
        if (handle != IntPtr.Zero)
        {
          NativeMethods.FreeLibrary(handle);
          isHcwIrBlastDllPresent = true;
        }
      }
      catch
      {
      }
    }

    public bool BlastChannelNumber(string channelNumber, int port)
    {
      return _blaster.BlastChannelNumber(channelNumber, port);
    }
  }
}