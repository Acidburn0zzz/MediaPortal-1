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

using System.Reflection;
using System.Runtime.InteropServices;
using MediaPortal.Common.Utils;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Tuner Extension - eMPIA")]
[assembly: AssemblyDescription("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("10aaf2b6-3744-4db0-9164-2b4d351254fa")]

// MediaPortal TV Server plugin compatibility.
[assembly: CompatibleVersion("1.2.3.0", "1.2.3.0")]
[assembly: UsesSubsystem("TVE.Common.Types")]
[assembly: UsesSubsystem("TVE.DirectShow")]
[assembly: UsesSubsystem("TVE.Plugins.TunerExtension")]
[assembly: UsesSubsystem("TVE.Plugins.TunerExtension.Diseqc")]
[assembly: UsesSubsystem("TVE.Plugins.TunerExtension.StreamSelector")]
[assembly: UsesSubsystem("TVE.Plugins.TunerExtension.Tuner")]