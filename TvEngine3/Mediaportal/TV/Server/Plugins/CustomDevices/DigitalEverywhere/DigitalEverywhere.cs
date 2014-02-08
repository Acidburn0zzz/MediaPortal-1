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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Diseqc;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Helper;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.DigitalEverywhere
{
  /// <summary>
  /// A class for handling conditional access, DiSEqC and PID filtering for Digital Everywhere tuners.
  /// </summary>
  public class DigitalEverywhere : BaseCustomDevice, IPowerDevice, IMpeg2PidFilter, ICustomTuner, IConditionalAccessProvider, IConditionalAccessMenuActions, IDiseqcDevice
  {
    #region enums

    private enum BdaExtensionProperty
    {
      SelectMultiplexDvbS = 0,
      SelectServiceDvbS,
      SelectPidsDvbS,                   // Use for DVB-S and DVB-C.
      SignalStrength,
      DriverVersion,
      SelectMultiplexDvbT,
      SelectPidsDvbT,
      SelectMultiplexDvbC,
      SelectPidsDvbC,                   // Don't use.
      FrontendStatus,
      SystemInfo,
      FirmwareVersion,
      LnbControl,
      GetLnbParams,
      SetLnbParams,
      LnbPower,
      AutoTuneStatus,
      FirmwareUpdate,
      FirmwareUpdateStatus,
      CiReset,
      CiWriteTpdu,
      CiReadTpdu,
      MmiHostToCam,
      MmiCamToHost,
      Temperature,
      TuneQpsk,
      RemoteControlRegister,
      RemoteControlCancel,
      CiStatus,
      TestInterface,
      CheckTuningFlag
    }

    private enum DeCiMessageTag : byte
    {
      Reset = 0,
      ApplicationInfo,
      Pmt,
      PmtReply,
      DateTime,
      Mmi,
      DebugError,
      EnterMenu,
      SendServiceId
    }

    private enum DeLnbPower : byte
    {
      Off = 0x60,
      On = 0x70
    }

    private enum DeResetType : byte
    {
      ForcedHardwareReset = 0
    }

    private enum DeErrorCode
    {
      Success = 0,
      Error,
      InvalidDeviceHandle,
      InvalidValue,
      AlreadyInUse,
      NotSupportedByTuner
    }

    private enum DeFecRate : byte
    {
      Auto = 0,
      Rate1_2,            // 1/2
      Rate2_3,            // 2/3
      Rate3_4,            // 3/4
      Rate5_6,            // 5/6
      Rate7_8             // 7/8
    }

    private enum DePolarisation : byte
    {
      None = 0xff,
      Horizontal = 0,
      Vertical = 1
    }

    private enum De22k : byte
    {
      Undefined = 0xff,
      Off = 0,
      On = 1
    }

    private enum DeToneBurst : byte
    {
      Undefined = 0xff,
      ToneBurst = 0,
      DataBurst = 1
    }

    private enum DeOfdmConstellation : byte
    {
      Auto = 0xff,
      DvbtQpsk = 0,
      Qam16,
      Qam64
    }

    private enum DeOfdmHierarchy : byte
    {
      Auto = 0xff,
      None = 0,
      One,
      Two,
      Four
    }

    private enum DeOfdmCodeRate : byte
    {
      Auto = 0xff,
      Rate1_2 = 0,        // 1/2
      Rate2_3,            // 2/3
      Rate3_4,            // 3/4
      Rate5_6,            // 5/6
      Rate7_8             // 7/8
    }

    private enum DeOfdmGuardInterval : byte
    {
      Auto = 0xff,
      Interval1_32 = 0,   // 1/32
      Interval1_16,       // 1/16
      Interval1_8,        // 1/8
      Interval1_4         // 1/4
    }

    private enum DeOfdmTransmissionMode : byte
    {
      Auto = 0xff,
      Mode2k = 0,
      Mode8k
    }

    private enum DeOfdmBandwidth : byte
    {
      Bandwidth8 = 0,     // 8 MHz
      Bandwidth7,         // 7 MHz
      Bandwidth6          // 6 MHz
    }

    [Flags]
    private enum DeFrontEndState : byte
    {
      PowerSupply = 0x01,
      PowerStatus = 0x02,
      AutoTune = 0x04,
      AntennaError = 0x08,
      FrontEndError = 0x10,
      VoltageValid = 0x40,
      FlagsValid = 0x80
    }

    [Flags]
    private enum DeCiState : ushort
    {
      Empty = 0,
      ErrorMessageAvailable = 0x0001,       // CI_ERR_MSG_AVAILABLE

      // CAM states.
      CamReady = 0x0002,                    // CI_MODULE_INIT_READY
      CamError = 0x0004,                    // CI_MODULE_ERROR
      CamIsDvb = 0x0008,                    // CI_MODULE_IS_DVB
      CamPresent = 0x0010,                  // CI_MODULE_PRESENT

      // MMI response states.
      ApplicationInfoAvailable = 0x0020,    // CI_APP_INFO_AVAILABLE - indicates whether the CAM is able to descramble or not
      DateTimeRequest = 0x0040,             // CI_DATE_TIME_REQEST
      PmtReply = 0x0080,                    // CI_PMT_REPLY
      MmiRequest = 0x0100                   // CI_MMI_REQUEST
    }

    private enum DeAntennaType : byte
    {
      Fixed = 0,
      Movable,
      Mobile
    }

    private enum DeBroadcastSystem : byte
    {
      Undefined = 0,
      DvbS = 0x01,
      DvbC = 0x02,
      DvbT = 0x03,
      AnalogAudio = 0x10,
      AnalogVideo = 0x11,
      Dvb = 0x20,
      Dab = 0x21,
      Atsc = 0x22
    }

    private enum DeTransportType : byte
    {
      Undefined = 0,
      Satellite,
      Cable,
      Terrestrial
    }

    #endregion

    #region structs

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DvbsMultiplexParams
    {
      public uint Frequency;              // unit = kHz, range = 9750000 - 12750000
      public uint SymbolRate;             // unit = ks/s, range = 1000 - 40000

      public DeFecRate InnerFecRate;
      public DePolarisation Polarisation;
      public byte Lnb;                    // index (0..3) of the LNB parameters set with SetLnbParams
      private byte Padding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DvbsServiceParams
    {
      [MarshalAs(UnmanagedType.Bool)]
      public bool CurrentTransponder;
      public uint Lnb;                    // index (0..3) of the LNB parameters set with SetLnbParams

      public uint Frequency;              // unit = kHz, range = 9750000 - 12750000
      public uint SymbolRate;             // unit = ks/s, range = 1000 - 40000

      public DeFecRate InnerFecRate;
      public DePolarisation Polarisation;
      private ushort Padding;

      public ushort OriginalNetworkId;
      public ushort TransportStreamId;
      public ushort ServiceId;
      public ushort VideoPid;
      public ushort AudioPid;
      public ushort PcrPid;
      public ushort TeletextPid;
      public ushort PmtPid;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DvbsPidFilterParams
    {
      [MarshalAs(UnmanagedType.Bool)]
      public bool CurrentTransponder;
      [MarshalAs(UnmanagedType.Bool)]
      public bool FullTransponder;

      public byte Lnb;                    // index (0..3) of the LNB parameters set with SetLnbParams
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
      private byte[] Padding1;

      public uint Frequency;              // unit = kHz, range = 9750000 - 12750000
      public uint SymbolRate;             // unit = ks/s, range = 1000 - 40000

      public DeFecRate InnerFecRate;
      public DePolarisation Polarisation;
      private ushort Padding2;

      public byte NumberOfValidPids;
      private byte Padding3;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_PID_FILTER_PID_COUNT)]
      public ushort[] FilterPids;
      private ushort Padding4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DvbtMultiplexParams
    {
      public uint Frequency;              // unit = kHz, range = 47000 - 860000
      public DeOfdmBandwidth Bandwidth;
      public DeOfdmConstellation Constellation;
      public DeOfdmCodeRate CodeRateHp;
      public DeOfdmCodeRate CodeRateLp;
      public DeOfdmGuardInterval GuardInterval;
      public DeOfdmTransmissionMode TransmissionMode;
      public DeOfdmHierarchy Hierarchy;
      private byte Padding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DvbtPidFilterParams
    {
      [MarshalAs(UnmanagedType.Bool)]
      public bool CurrentTransponder;
      [MarshalAs(UnmanagedType.Bool)]
      public bool FullTransponder;

      public DvbtMultiplexParams MultiplexParams;

      public byte NumberOfValidPids;
      private byte Padding1;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_PID_FILTER_PID_COUNT)]
      public ushort[] FilterPids;
      private ushort Padding2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FirmwareVersionInfo
    {
      public byte HardwareMajor;
      public byte HardwareMiddle;
      public byte HardwareMinor;
      public byte SoftwareMajor;
      public byte SoftwareMiddle;
      public byte SoftwareMinor;
      public byte BuildNumberMsb;
      public byte BuildNumberLsb;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FrontEndStatusInfo
    {
      public uint Frequency;              // unit = kHz, intermediate frequency for DVB-S/2
      public uint BitErrorRate;

      public byte SignalStrength;         // range = 0 - 100%
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
      private byte[] Padding1;
      [MarshalAs(UnmanagedType.Bool)]
      public bool IsLocked;

      public ushort CarrierToNoiseRatio;
      public byte AutomaticGainControl;
      private byte Value;                 // ???

      public DeFrontEndState FrontEndState;
      private byte Padding2;
      public DeCiState CiState;

      public byte SupplyVoltage;
      public byte AntennaVoltage;
      public byte BusVoltage;
      private byte Padding3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SystemInfo
    {
      public byte NumberOfAntennas;       // range = 0 - 3
      public DeAntennaType AntennaType;
      public DeBroadcastSystem BroadcastSystem;
      public DeTransportType TransportType;

      [MarshalAs(UnmanagedType.Bool)]
      public bool Lists;                  // ???
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DiseqcMessage
    {
      public byte MessageLength;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DISEQC_MESSAGE_LENGTH)]
      public byte[] Message;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct LnbCommand
    {
      public byte Voltage;
      public De22k Tone22k;
      public DeToneBurst ToneBurst;
      public byte NumberOfMessages;

      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DISEQC_MESSAGE_COUNT)]
      public DiseqcMessage[] DiseqcMessages;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct LnbParams
    {
      public uint AntennaNumber;
      [MarshalAs(UnmanagedType.Bool)]
      public bool IsEast;

      public ushort OrbitalPosition;
      public ushort LowBandLof;             // unit = MHz
      public ushort SwitchFrequency;        // unit = MHz
      public ushort HighBandLof;            // unit = MHz
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct LnbParamInfo
    {
      public int NumberOfAntennas;         // range = 0 - 3
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_LNB_PARAM_COUNT)]
      public LnbParams[] LnbParams;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct QpskTuneParams
    {
      public uint Frequency;                // unit = kHz, range = 950000 - 2150000

      public ushort SymbolRate;             // unit = ks/s, range = 1000 - 40000
      public DeFecRate InnerFecRate;
      public DePolarisation Polarisation;

      [MarshalAs(UnmanagedType.Bool)]
      public bool IsHighBand;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct CiErrorDebugMessage
    {
      public byte MessageType;
      public byte MessageLength;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_CI_ERROR_DEBUG_MESSAGE_LENGTH)]
      public string Message;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct CaData
    {
      public byte Slot;
      public DeCiMessageTag Tag;
      private ushort Padding1;

      [MarshalAs(UnmanagedType.Bool)]
      public bool More;

      public ushort DataLength;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_PMT_LENGTH)]
      public byte[] Data;
      private ushort Padding2;

      public CaData(DeCiMessageTag tag)
      {
        Slot = 0;
        Tag = tag;
        Padding1 = 0;
        More = false;
        DataLength = 0;
        Data = new byte[MAX_PMT_LENGTH];
        Padding2 = 0;
      }
    }

    #endregion

    #region constants

    private static readonly Guid BDA_EXTENSION_PROPERTY_SET = new Guid(0xab132414, 0xd060, 0x11d0, 0x85, 0x83, 0x00, 0xc0, 0x4f, 0xd9, 0xba, 0xf3);

    private const int MAX_PMT_LENGTH = 1024;
    private static readonly int DVBS_MULTIPLEX_PARAMS_SIZE = Marshal.SizeOf(typeof(DvbsMultiplexParams));     // 12
    private static readonly int DVBS_SERVICE_PARAMS_SIZE = Marshal.SizeOf(typeof(DvbsServiceParams));         // 36
    private static readonly int DVBS_PID_FILTER_PARAMS_SIZE = Marshal.SizeOf(typeof(DvbsPidFilterParams));    // 60
    private static readonly int DVBT_MULTIPLEX_PARAMS_SIZE = Marshal.SizeOf(typeof(DvbtMultiplexParams));     // 12
    private static readonly int DVBT_PID_FILTER_PARAMS_SIZE = Marshal.SizeOf(typeof(DvbtPidFilterParams));    // 56
    private const int MAX_PID_FILTER_PID_COUNT = 16;
    private static readonly int FIRMWARE_VERSION_INFO_SIZE = Marshal.SizeOf(typeof(FirmwareVersionInfo));     // 8
    private static readonly int FRONT_END_STATUS_INFO_SIZE = Marshal.SizeOf(typeof(FrontEndStatusInfo));      // 28
    private static readonly int SYSTEM_INFO_SIZE = Marshal.SizeOf(typeof(SystemInfo));            // 8
    private static readonly int DISEQC_MESSAGE_SIZE = Marshal.SizeOf(typeof(DiseqcMessage));      // 7
    private const int MAX_DISEQC_MESSAGE_LENGTH = 6;
    private static readonly int LNB_COMMAND_SIZE = Marshal.SizeOf(typeof(LnbCommand));            // 25
    private const int MAX_DISEQC_MESSAGE_COUNT = 3;
    private static readonly int LNB_PARAMS_SIZE = Marshal.SizeOf(typeof(LnbParams));              // 16
    private static readonly int LNB_PARAM_INFO_SIZE = Marshal.SizeOf(typeof(LnbParamInfo));       // 68
    private const int MAX_LNB_PARAM_COUNT = 4;
    private static readonly int QPSK_TUNE_PARAMS_SIZE = Marshal.SizeOf(typeof(QpskTuneParams));   // 12
    private static readonly int CI_ERROR_DEBUG_MESSAGE_LENGTH = Marshal.SizeOf(typeof(CiErrorDebugMessage));  // 258
    private const int MAX_CI_ERROR_DEBUG_MESSAGE_LENGTH = 256;
    private static readonly int CA_DATA_SIZE = Marshal.SizeOf(typeof(CaData));  // 1036
    private const int DRIVER_VERSION_INFO_SIZE = 32;
    private const int TEMPERATURE_INFO_SIZE = 4;

    private static readonly int GENERAL_BUFFER_SIZE = new int[]
      {
        CA_DATA_SIZE, DRIVER_VERSION_INFO_SIZE, DVBS_MULTIPLEX_PARAMS_SIZE, DVBT_MULTIPLEX_PARAMS_SIZE,
        FIRMWARE_VERSION_INFO_SIZE, FRONT_END_STATUS_INFO_SIZE, LNB_COMMAND_SIZE, LNB_PARAM_INFO_SIZE,
        TEMPERATURE_INFO_SIZE
      }.Max();
    private const int MMI_HANDLER_THREAD_WAIT_TIME = 500;     // unit = ms

    #endregion

    #region variables

    private bool _isDigitalEverywhere = false;
    private bool _isCaInterfaceOpen = false;
    #pragma warning disable 0414
    private bool _isCamPresent = false;
    #pragma warning restore 0414
    private bool _isCamReady = false;
    private HashSet<ushort> _pidFilterPids = new HashSet<ushort>();

    private IntPtr _generalBuffer = IntPtr.Zero;
    private IntPtr _mmiBuffer = IntPtr.Zero;
    private IntPtr _pmtBuffer = IntPtr.Zero;

    private IKsPropertySet _propertySet = null;
    private CardType _tunerType = CardType.Unknown;

    private Thread _mmiHandlerThread = null;
    private AutoResetEvent _mmiHandlerThreadStopEvent = null;
    private object _mmiLock = new object();
    private IConditionalAccessMenuCallBacks _caMenuCallBacks = null;
    private object _caMenuCallBackLock = new object();

    #endregion

    /// <summary>
    /// Get the conditional access interface status.
    /// </summary>
    /// <param name="ciState">State of the CI slot.</param>
    /// <returns>an HRESULT indicating whether the CI status was successfully retrieved</returns>
    private int GetCiStatus(out DeCiState ciState)
    {
      ciState = DeCiState.Empty;

      // Use a local buffer here because this function is called from the MMI
      // polling thread as well as indirectly from the main TV service thread.
      int bufferSize = sizeof(DeCiState);   // 2 bytes
      IntPtr responsebuffer = Marshal.AllocCoTaskMem(bufferSize);
      int hr = (int)HResult.Severity.Error;
      try
      {
        for (int i = 0; i < bufferSize; i++)
        {
          Marshal.WriteByte(responsebuffer, i, 0);
        }
        int returnedByteCount;
        hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.CiStatus,
          responsebuffer, bufferSize,
          responsebuffer, bufferSize,
          out returnedByteCount
        );
        if (hr == (int)HResult.Severity.Success && returnedByteCount == bufferSize)
        {
          ciState = (DeCiState)Marshal.ReadInt16(responsebuffer, 0);
        }
      }
      finally
      {
        Marshal.FreeCoTaskMem(responsebuffer);
      }
      return hr;
    }

    /// <summary>
    /// Send an MMI message to the CAM.
    /// </summary>
    /// <param name="data">The message.</param>
    /// <returns><c>true</c> if the message is successfully sent, otherwise <c>false</c></returns>
    private bool SendMmi(CaData data)
    {
      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }
      StartMmiHandlerThread();
      if (!_isCamReady)
      {
        this.LogError("Digital Everywhere: the CAM is not ready");
        return false;
      }

      int hr;
      lock (_mmiLock)
      {
        Marshal.StructureToPtr(data, _mmiBuffer, true);
        hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.MmiHostToCam,
          _mmiBuffer, CA_DATA_SIZE,
          _mmiBuffer, CA_DATA_SIZE
        );
      }
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      this.LogError("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #region hardware/software information

    /// <summary>
    /// Attempt to read the driver information from the tuner.
    /// </summary>
    private void ReadDriverInfo()
    {
      this.LogDebug("Digital Everywhere: read driver information");
      for (int i = 0; i < DRIVER_VERSION_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      int returnedByteCount;
      int hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.DriverVersion,
        _generalBuffer, DRIVER_VERSION_INFO_SIZE,
        _generalBuffer, DRIVER_VERSION_INFO_SIZE,
        out returnedByteCount
      );
      if (hr != (int)HResult.Severity.Success || returnedByteCount != DRIVER_VERSION_INFO_SIZE)
      {
        this.LogWarn("Digital Everywhere: result = failure, hr = 0x{0:x} ({1}), byte count = {2}", hr, HResult.GetDXErrorString(hr), returnedByteCount);
        return;
      }

      //Dump.DumpBinary(_generalBuffer, returnedByteCount);
      this.LogDebug("  driver version   = {0}", Marshal.PtrToStringAnsi(_generalBuffer));
    }

    /// <summary>
    /// Attempt to read the hardware and firmware information from the tuner.
    /// </summary>
    private void ReadHardwareInfo()
    {
      this.LogDebug("Digital Everywhere: read hardware/firmware information");
      for (int i = 0; i < FIRMWARE_VERSION_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      int returnedByteCount;
      int hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.FirmwareVersion,
        _generalBuffer, FIRMWARE_VERSION_INFO_SIZE,
        _generalBuffer, FIRMWARE_VERSION_INFO_SIZE,
        out returnedByteCount
      );
      if (hr != (int)HResult.Severity.Success || returnedByteCount != FIRMWARE_VERSION_INFO_SIZE)
      {
        this.LogWarn("Digital Everywhere: result = failure, hr = 0x{0:x} ({1}), byte count = {2}", hr, HResult.GetDXErrorString(hr), returnedByteCount);
        return;
      }

      byte[] b = { 0, 0, 0, 0, 0, 0, 0, 0 };
      Marshal.Copy(_generalBuffer, b, 0, 8);
      this.LogDebug("  hardware version = {0:x}.{1:x}.{2:x2}", b[0], b[1], b[2]);
      this.LogDebug("  firmware version = {0:x}.{1:x}.{2:x2}", b[3], b[4], b[5]);
      this.LogDebug("  firmware build # = {0}", (b[6] * 256) + b[7]);
    }

    /// <summary>
    /// Attempt to read the temperature from the tuner.
    /// </summary>
    private void ReadTemperature()
    {
      this.LogDebug("Digital Everywhere: read temperature");
      for (int i = 0; i < TEMPERATURE_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      int returnedByteCount;
      int hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.Temperature,
        _generalBuffer, TEMPERATURE_INFO_SIZE,
        _generalBuffer, TEMPERATURE_INFO_SIZE,
        out returnedByteCount
      );
      if (hr != (int)HResult.Severity.Success || returnedByteCount != TEMPERATURE_INFO_SIZE)
      {
        this.LogWarn("Digital Everywhere: result = failure, hr = 0x{0:x} ({1}), byte count = {2}", hr, HResult.GetDXErrorString(hr), returnedByteCount);
        return;
      }

      // The output is all-zeroes for my FloppyDTV-S2 with the following details:
      //   driver version   = 5.0 (6201-3000) x64
      //   hardware version = 1.24.04
      //   firmware version = 1.5.02
      //   firmware build # = 30740
      Dump.DumpBinary(_generalBuffer, TEMPERATURE_INFO_SIZE);
    }

    /// <summary>
    /// Attempt to read the front end status from the tuner.
    /// </summary>
    private void ReadFrontEndStatus()
    {
      this.LogDebug("Digital Everywhere: read front end status information");
      for (int i = 0; i < FRONT_END_STATUS_INFO_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      int returnedByteCount;
      int hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.FrontendStatus,
        _generalBuffer, FRONT_END_STATUS_INFO_SIZE,
        _generalBuffer, FRONT_END_STATUS_INFO_SIZE,
        out returnedByteCount
      );
      if (hr != (int)HResult.Severity.Success || returnedByteCount != FRONT_END_STATUS_INFO_SIZE)
      {
        this.LogWarn("Digital Everywhere: result = failure, hr = 0x{0:x} ({1}), byte count = {2}", hr, HResult.GetDXErrorString(hr), returnedByteCount);
        return;
      }

      // Most of this info is not very useful.
      //Dump.DumpBinary(_generalBuffer, FRONT_END_STATUS_INFO_SIZE);
      FrontEndStatusInfo status = (FrontEndStatusInfo)Marshal.PtrToStructure(_generalBuffer, typeof(FrontEndStatusInfo));
      this.LogDebug("  frequency        = {0} kHz", status.Frequency);
      this.LogDebug("  bit error rate   = {0}", status.BitErrorRate);
      this.LogDebug("  signal strength  = {0}", status.SignalStrength);
      this.LogDebug("  is locked        = {0}", status.IsLocked);
      this.LogDebug("  CNR              = {0}", status.CarrierToNoiseRatio);
      this.LogDebug("  auto gain ctrl   = {0}", status.AutomaticGainControl);
      this.LogDebug("  front end state  = {0}", status.FrontEndState.ToString());
      this.LogDebug("  CI state         = {0}", status.CiState.ToString());
      this.LogDebug("  supply voltage   = {0}", status.SupplyVoltage);
      this.LogDebug("  antenna voltage  = {0}", status.AntennaVoltage);
      this.LogDebug("  bus voltage      = {0}", status.BusVoltage);
    }

    /// <summary>
    /// Read the conditional access application information from the CAM.
    /// </summary>
    private void ReadApplicationInformation()
    {
      this.LogDebug("Digital Everywhere: request application information");
      CaData data = new CaData(DeCiMessageTag.ApplicationInfo);
      Marshal.StructureToPtr(data, _generalBuffer, true);
      int hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.MmiCamToHost,
        _generalBuffer, CA_DATA_SIZE,
        _generalBuffer, CA_DATA_SIZE
      );
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogWarn("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return;
      }

      this.LogDebug("Digital Everywhere: read application information");
      for (int i = 0; i < CA_DATA_SIZE; i++)
      {
        Marshal.WriteByte(_generalBuffer, i, 0);
      }
      int returnedByteCount;
      hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.MmiCamToHost,
        _generalBuffer, CA_DATA_SIZE,
        _generalBuffer, CA_DATA_SIZE,
        out returnedByteCount
      );
      if (hr != (int)HResult.Severity.Success || returnedByteCount != CA_DATA_SIZE)
      {
        this.LogWarn("Digital Everywhere: result = failure, hr = 0x{0:x} ({1}), byte count = {2}", hr, HResult.GetDXErrorString(hr), returnedByteCount);
        return;
      }

      data = (CaData)Marshal.PtrToStructure(_generalBuffer, typeof(CaData));
      this.LogDebug("  manufacturer = 0x{0:x}{1:x}", data.Data[0], data.Data[1]);
      this.LogDebug("  code         = 0x{0:x}{1:x}", data.Data[2], data.Data[3]);
      this.LogDebug("  menu title   = {0}", DvbTextConverter.Convert(data.Data, data.Data[4], 5));
    }

    #endregion

    #region MMI handler thread

    /// <summary>
    /// Start a thread to receive MMI messages from the CAM.
    /// </summary>
    private void StartMmiHandlerThread()
    {
      // Don't start a thread if the interface has not been opened.
      if (!_isCaInterfaceOpen)
      {
        return;
      }

      lock (_mmiLock)
      {
        // Kill the existing thread if it is in "zombie" state.
        if (_mmiHandlerThread != null && !_mmiHandlerThread.IsAlive)
        {
          StopMmiHandlerThread();
        }

        if (_mmiHandlerThread == null)
        {
          this.LogDebug("Digital Everywhere: starting new MMI handler thread");
          _mmiHandlerThreadStopEvent = new AutoResetEvent(false);
          _mmiHandlerThread = new Thread(new ThreadStart(MmiHandler));
          _mmiHandlerThread.Name = "Digital Everywhere MMI handler";
          _mmiHandlerThread.IsBackground = true;
          _mmiHandlerThread.Priority = ThreadPriority.Lowest;
          _mmiHandlerThread.Start();
        }
      }
    }

    /// <summary>
    /// Stop the thread that receives MMI messages from the CAM.
    /// </summary>
    private void StopMmiHandlerThread()
    {
      lock (_mmiLock)
      {
        if (_mmiHandlerThread != null)
        {
          if (!_mmiHandlerThread.IsAlive)
          {
            this.LogWarn("Digital Everywhere: aborting old MMI handler thread");
            _mmiHandlerThread.Abort();
          }
          else
          {
            _mmiHandlerThreadStopEvent.Set();
            if (!_mmiHandlerThread.Join(MMI_HANDLER_THREAD_WAIT_TIME * 2))
            {
              this.LogWarn("Digital Everywhere: failed to join MMI handler thread, aborting thread");
              _mmiHandlerThread.Abort();
            }
          }
          _mmiHandlerThread = null;
          if (_mmiHandlerThreadStopEvent != null)
          {
            _mmiHandlerThreadStopEvent.Close();
            _mmiHandlerThreadStopEvent = null;
          }
        }
      }
    }

    /// <summary>
    /// Thread function for receiving MMI messages from the CAM.
    /// </summary>
    private void MmiHandler()
    {
      this.LogDebug("Digital Everywhere: MMI handler thread start polling");
      DeCiState ciState = DeCiState.Empty;
      DeCiState prevCiState = DeCiState.Empty;

      try
      {
        while (!_mmiHandlerThreadStopEvent.WaitOne(MMI_HANDLER_THREAD_WAIT_TIME))
        {
          int hr = GetCiStatus(out ciState);
          if (hr != (int)HResult.Severity.Success)
          {
            this.LogError("Digital Everywhere: failed to get CI status, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
            continue;
          }

          // Handle CI slot state changes.
          if (ciState != prevCiState)
          {
            this.LogInfo("Digital Everywhere: CI state change");
            this.LogInfo("  old state = {0}", prevCiState.ToString());
            this.LogInfo("  new state = {0}", ciState.ToString());
            prevCiState = ciState;

            if (ciState.HasFlag(DeCiState.CamPresent | DeCiState.CamIsDvb))
            {
              _isCamPresent = true;
              if (!ciState.HasFlag(DeCiState.CamError) &&
                ciState.HasFlag(DeCiState.CamReady | DeCiState.ApplicationInfoAvailable))
              {
                _isCamReady = true;
              }
              else
              {
                _isCamReady = false;
              }
            }
            else
            {
              _isCamPresent = false;
              _isCamReady = false;
            }
          }

          // If there is no CAM present or the CAM is not ready for interaction
          // then don't attempt to communicate with the CI.
          if (!_isCamReady)
          {
            continue;
          }

          // Check for MMI responses and requests.
          if (ciState.HasFlag(DeCiState.MmiRequest))
          {
            this.LogDebug("Digital Everywhere: MMI data available, sending request");
            CaData data = new CaData(DeCiMessageTag.Mmi);
            lock (_mmiLock)
            {
              Marshal.StructureToPtr(data, _mmiBuffer, true);
              hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.MmiCamToHost,
                _mmiBuffer, CA_DATA_SIZE,
                _mmiBuffer, CA_DATA_SIZE
              );
            }
            if (hr != (int)HResult.Severity.Success)
            {
              this.LogError("Digital Everywhere: request failed, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
              continue;
            }

            this.LogDebug("Digital Everywhere: retrieving data");
            lock (_mmiLock)
            {
              for (int i = 0; i < CA_DATA_SIZE; i++)
              {
                Marshal.WriteByte(_mmiBuffer, i, 0);
              }
              int returnedByteCount;
              hr = _propertySet.Get(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.MmiCamToHost,
                _mmiBuffer, CA_DATA_SIZE,
                _mmiBuffer, CA_DATA_SIZE,
                out returnedByteCount
              );
              if (hr != (int)HResult.Severity.Success || returnedByteCount != CA_DATA_SIZE)
              {
                this.LogError("Digital Everywhere: failed to retrieve data, hr = 0x{0:x} ({1}), byte count = {2}", hr, HResult.GetDXErrorString(hr), returnedByteCount);
                continue;
              }

              this.LogDebug("Digital Everywhere: handling data");
              data = (CaData)Marshal.PtrToStructure(_mmiBuffer, typeof(CaData));
            }
            byte[] objectData = new byte[data.DataLength];
            Array.Copy(data.Data, objectData, data.DataLength);
            lock (_caMenuCallBackLock)
            {
              DvbMmiHandler.HandleMmiData(objectData, _caMenuCallBacks);
            }
          }
        }
      }
      catch (ThreadAbortException)
      {
      }
      catch (Exception ex)
      {
        this.LogError(ex, "Digital Everywhere: MMI handler thread exception");
        return;
      }
    }

    #endregion

    #region ICustomDevice members

    /// <summary>
    /// A human-readable name for the extension. This could be a manufacturer or reseller name, or
    /// even a model name and/or number.
    /// </summary>
    public override string Name
    {
      get
      {
        return "Digital Everywhere";
      }
    }

    /// <summary>
    /// Attempt to initialise the extension-specific interfaces used by the class. If
    /// initialisation fails, the <see ref="ICustomDevice"/> instance should be disposed
    /// immediately.
    /// </summary>
    /// <param name="tunerExternalIdentifier">The external identifier for the tuner.</param>
    /// <param name="tunerType">The tuner type (eg. DVB-S, DVB-T... etc.).</param>
    /// <param name="context">Context required to initialise the interface.</param>
    /// <returns><c>true</c> if the interfaces are successfully initialised, otherwise <c>false</c></returns>
    public override bool Initialise(string tunerExternalIdentifier, CardType tunerType, object context)
    {
      this.LogDebug("Digital Everywhere: initialising");

      if (context == null)
      {
        this.LogDebug("Digital Everywhere: context is null");
        return false;
      }
      if (_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: extension already initialised");
        return true;
      }

      _propertySet = context as IKsPropertySet;
      if (_propertySet == null)
      {
        this.LogDebug("Digital Everywhere: context is not a property set");
        return false;
      }

      int hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.TestInterface,
        IntPtr.Zero, 0, IntPtr.Zero, 0
      );
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: property set not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      this.LogInfo("Digital Everywhere: extension supported");
      _isDigitalEverywhere = true;
      _tunerType = tunerType;
      _generalBuffer = Marshal.AllocCoTaskMem(GENERAL_BUFFER_SIZE);

      ReadDriverInfo();
      ReadHardwareInfo();
      ReadTemperature();
      ReadFrontEndStatus();
      return true;
    }

    #region device state change call backs

    /// <summary>
    /// This call back is invoked before a tune request is assembled.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is currently tuned to..</param>
    /// <param name="channel">The channel that the tuner will been tuned to.</param>
    /// <param name="action">The action to take, if any.</param>
    public override void OnBeforeTune(ITVCard tuner, IChannel currentChannel, ref IChannel channel, out TunerAction action)
    {
      this.LogDebug("Digital Everywhere: on before tune call back");
      action = TunerAction.Default;

      if (!_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return;
      }

      // We need to tweak the modulation and inner FEC rate, but only for DVB-S/2 channels.
      DVBSChannel ch = channel as DVBSChannel;
      if (ch == null)
      {
        return;
      }
      if (ch.ModulationType == ModulationType.ModQpsk)
      {
        ch.ModulationType = ModulationType.ModNbcQpsk;
      }
      else if (ch.ModulationType == ModulationType.Mod8Psk)
      {
        ch.ModulationType = ModulationType.ModNbc8Psk;
      }
      else if (ch.ModulationType == ModulationType.ModNotSet)
      {
        ch.ModulationType = ModulationType.ModQpsk;
        // For DVB-S, pilot and roll-off should be "not set", however we're not
        // going to force this.
      }
      this.LogDebug("  modulation     = {0}", ch.ModulationType);

      if (ch.InnerFecRate != BinaryConvolutionCodeRate.RateNotSet)
      {
        // Digital Everywhere uses the inner FEC rate parameter to encode the
        // pilot and roll-off as well as the FEC rate.
        int rate = (int)ch.InnerFecRate;
        if (ch.Pilot == Pilot.Off)
        {
          rate += 64;
        }
        else if (ch.Pilot == Pilot.On)
        {
          rate += 128;
        }

        if (ch.RollOff == RollOff.Twenty)
        {
          rate += 16;
        }
        else if (ch.RollOff == RollOff.TwentyFive)
        {
          rate += 32;
        }
        else if (ch.RollOff == RollOff.ThirtyFive)
        {
          rate += 48;
        }
        ch.InnerFecRate = (BinaryConvolutionCodeRate)rate;
      }
      this.LogDebug("  inner FEC rate = {0}", ch.InnerFecRate);
    }

    /// <summary>
    /// This call back is invoked after a tune request is submitted, when the tuner is started but
    /// before signal lock is checked.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is tuned to.</param>
    public override void OnStarted(ITVCard tuner, IChannel currentChannel)
    {
      // Ensure the MMI handler thread is always running when the graph is running.
      StartMmiHandlerThread();
    }

    #endregion

    #endregion

    #region IPowerDevice member

    /// <summary>
    /// Set the tuner power state.
    /// </summary>
    /// <param name="state">The power state to apply.</param>
    /// <returns><c>true</c> if the power state is set successfully, otherwise <c>false</c></returns>
    public bool SetPowerState(PowerState state)
    {
      this.LogDebug("Digital Everywhere: set power state, state = {0}", state);

      if (!_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }

      // The FloppyDTV and FireDTV S and S2 support this function; the other Digital Everywhere tuners do not.
      // Apparently the FireDTV T also supports active antennas but it is unclear whether and how that power
      // supply might be turned on or off.
      KSPropertySupport support;
      int hr = _propertySet.QuerySupported(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.LnbPower, out support);
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Set))
      {
        this.LogDebug("Digital Everywhere: property not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      if (state == PowerState.On)
      {
        Marshal.WriteByte(_generalBuffer, 0, (byte)DeLnbPower.On);
      }
      else
      {
        Marshal.WriteByte(_generalBuffer, 0, (byte)DeLnbPower.Off);
      }
      hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.LnbPower,
        _generalBuffer, sizeof(Byte),
        _generalBuffer, sizeof(Byte)
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      this.LogError("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IMpeg2PidFilter member

    /// <summary>
    /// Should the filter be enabled for the current multiplex.
    /// </summary>
    /// <param name="tuningDetail">The current multiplex/transponder tuning parameters.</param>
    /// <returns><c>true</c> if the filter should be enabled, otherwise <c>false</c></returns>
    public bool ShouldEnableFilter(IChannel tuningDetail)
    {
      if (_tunerType != CardType.DvbS && _tunerType != CardType.DvbT && _tunerType != CardType.DvbC)
      {
        // PID filtering not supported.
        return false;
      }

      // It is not ideal to have to enable PID filtering because doing so can limit
      // the number of channels that can be viewed/recorded simultaneously. However,
      // it does seem that there is a need for filtering on satellite transponders
      // with high bit rates. Problems have been observed with transponders on Thor
      // 5/6, Intelsat 10-02 (0.8W) if the filter is not enabled:
      //   Symbol Rate: 27500, Modulation: 8 PSK, FEC rate: 5/6, Pilot: On, Roll-Off: 0.35
      //   Symbol Rate: 30000, Modulation: 8 PSK, FEC rate: 3/4, Pilot: On, Roll-Off: 0.35
      int bitRate = 0;
      DVBSChannel satelliteTuningDetail = tuningDetail as DVBSChannel;
      if (satelliteTuningDetail != null)
      {
        int bitsPerSymbol = 2;
        if (satelliteTuningDetail.ModulationType == ModulationType.ModBpsk)
        {
          bitsPerSymbol = 1;
        }
        else if (satelliteTuningDetail.ModulationType == ModulationType.Mod8Psk ||
          satelliteTuningDetail.ModulationType == ModulationType.ModNbc8Psk)
        {
          bitsPerSymbol = 3;
        }
        else if (satelliteTuningDetail.ModulationType == ModulationType.Mod16Apsk)
        {
          bitsPerSymbol = 4;
        }
        else if (satelliteTuningDetail.ModulationType == ModulationType.Mod32Apsk)
        {
          bitsPerSymbol = 5;
        }
        bitRate = bitsPerSymbol * satelliteTuningDetail.SymbolRate; // kb/s
        if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate1_2)
        {
          bitRate /= 2;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate1_3)
        {
          bitRate /= 3;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate1_4)
        {
          bitRate /= 4;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate2_3)
        {
          bitRate = bitRate * 2 / 3;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate2_5)
        {
          bitRate = bitRate * 2 / 5;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate3_4)
        {
          bitRate = bitRate * 3 / 4;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate3_5)
        {
          bitRate = bitRate * 3 / 5;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate4_5)
        {
          bitRate = bitRate * 4 / 5;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate5_6)
        {
          bitRate = bitRate * 5 / 6;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate5_11)
        {
          bitRate = bitRate * 5 / 11;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate6_7)
        {
          bitRate = bitRate * 6 / 7;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate7_8)
        {
          bitRate = bitRate * 7 / 8;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate8_9)
        {
          bitRate = bitRate * 8 / 9;
        }
        else if (satelliteTuningDetail.InnerFecRate == BinaryConvolutionCodeRate.Rate9_10)
        {
          bitRate = bitRate * 9 / 10;
        }
      }
      else
      {
        DVBCChannel cableTuningDetail = tuningDetail as DVBCChannel;
        double bitsPerSymbol = 6;  // 64 QAM
        if (cableTuningDetail.ModulationType == ModulationType.Mod80Qam)
        {
          bitsPerSymbol = 6.25;
        }
        else if (cableTuningDetail.ModulationType == ModulationType.Mod96Qam)
        {
          bitsPerSymbol = 6.5;
        }
        else if (cableTuningDetail.ModulationType == ModulationType.Mod112Qam)
        {
          bitsPerSymbol = 6.75;
        }
        else if (cableTuningDetail.ModulationType == ModulationType.Mod128Qam)
        {
          bitsPerSymbol = 7;
        }
        else if (cableTuningDetail.ModulationType == ModulationType.Mod160Qam)
        {
          bitsPerSymbol = 7.25;
        }
        else if (cableTuningDetail.ModulationType == ModulationType.Mod192Qam)
        {
          bitsPerSymbol = 7.5;
        }
        else if (cableTuningDetail.ModulationType == ModulationType.Mod224Qam)
        {
          bitsPerSymbol = 7.75;
        }
        else if (cableTuningDetail.ModulationType == ModulationType.Mod256Qam)
        {
          bitsPerSymbol = 8;
        }
        bitRate = (int)bitsPerSymbol * cableTuningDetail.SymbolRate;  // kb/s
      }

      // Rough approximation: enable PID filtering when bit rate is over 60 Mb/s.
      this.LogDebug("Digital Everywhere: multiplex bit rate = {0} kb/s", bitRate);
      return bitRate >= 60000;
    }

    /// <summary>
    /// Disable the filter.
    /// </summary>
    /// <returns><c>true</c> if the filter is successfully disabled, otherwise <c>false</c></returns>
    public bool DisableFilter()
    {
      this.LogDebug("Digital Everywhere: disable PID filter");
      if (!_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }

      _pidFilterPids.Clear();
      int hr = ConfigurePidFilter(false);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      this.LogError("Digital Everywhere: failed to diable PID filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Get the maximum number of streams that the filter can allow.
    /// </summary>
    public int MaximumPidCount
    {
      get
      {
        return MAX_PID_FILTER_PID_COUNT;
      }
    }

    /// <summary>
    /// Configure the filter to allow one or more streams to pass through the filter.
    /// </summary>
    /// <param name="pids">A collection of stream identifiers.</param>
    /// <returns><c>true</c> if the filter is successfully configured, otherwise <c>false</c></returns>
    public bool AllowStreams(ICollection<ushort> pids)
    {
      _pidFilterPids.UnionWith(pids);
      return true;
    }

    /// <summary>
    /// Configure the filter to stop one or more streams from passing through the filter.
    /// </summary>
    /// <param name="pids">A collection of stream identifiers.</param>
    /// <returns><c>true</c> if the filter is successfully configured, otherwise <c>false</c></returns>
    public bool BlockStreams(ICollection<ushort> pids)
    {
      _pidFilterPids.ExceptWith(pids);
      return true;
    }

    /// <summary>
    /// Apply the current filter configuration.
    /// </summary>
    /// <returns><c>true</c> if the filter configuration is successfully applied, otherwise <c>false</c></returns>
    public bool ApplyFilter()
    {
      this.LogDebug("Digital Everywhere: apply PID filter");

      int hr = ConfigurePidFilter(true);
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      this.LogError("Digital Everywhere: failed to apply PID filter, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    private int ConfigurePidFilter(bool enable)
    {
      ushort[] pids = new ushort[MAX_PID_FILTER_PID_COUNT];
      _pidFilterPids.CopyTo(pids, 0, Math.Min(_pidFilterPids.Count, MAX_PID_FILTER_PID_COUNT));

      BdaExtensionProperty property = BdaExtensionProperty.SelectPidsDvbS;
      int bufferSize = DVBS_PID_FILTER_PARAMS_SIZE;
      if (_tunerType == CardType.DvbS)
      {
        DvbsPidFilterParams filter = new DvbsPidFilterParams();
        filter.CurrentTransponder = true;
        filter.FullTransponder = !enable;
        filter.NumberOfValidPids = (byte)_pidFilterPids.Count;
        filter.FilterPids = pids;
        Marshal.StructureToPtr(filter, _generalBuffer, true);
      }
      else if (_tunerType == CardType.DvbT || _tunerType == CardType.DvbC)
      {
        property = BdaExtensionProperty.SelectPidsDvbT;
        bufferSize = DVBT_PID_FILTER_PARAMS_SIZE;
        DvbtPidFilterParams filter = new DvbtPidFilterParams();
        filter.CurrentTransponder = true;
        filter.FullTransponder = !enable;
        filter.NumberOfValidPids = (byte)_pidFilterPids.Count;
        filter.FilterPids = pids;
        Marshal.StructureToPtr(filter, _generalBuffer, true);
      }

      //Dump.DumpBinary(_generalBuffer, bufferSize);

      return _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)property,
        _generalBuffer, bufferSize,
        _generalBuffer, bufferSize
      );
    }

    #endregion

    #region ICustomTuner members

    /// <summary>
    /// Check if the extension implements specialised tuning for a given channel.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the extension supports specialised tuning for the channel, otherwise <c>false</c></returns>
    public bool CanTuneChannel(IChannel channel)
    {
      // Tuning of DVB-S/2 and DVB-T/2 channels is supported with an appropriate tuner. DVB-C tuning may also be
      // supported but documentation is missing.
      if ((channel is DVBSChannel && _tunerType == CardType.DvbS) || (channel is DVBTChannel && _tunerType == CardType.DvbT))
      {
        return true;
      }
      return false;
    }

    /// <summary>
    /// Tune to a given channel using the specialised tuning method.
    /// </summary>
    /// <param name="channel">The channel to tune.</param>
    /// <returns><c>true</c> if the channel is successfully tuned, otherwise <c>false</c></returns>
    public bool Tune(IChannel channel)
    {
      this.LogDebug("Digital Everywhere: tune to channel");

      if (!_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }

      int hr;

      DVBSChannel dvbsChannel = channel as DVBSChannel;
      if (dvbsChannel != null && _tunerType == CardType.DvbS)
      {
        // LNB settings must be applied.
        LnbParamInfo lnbParams = new LnbParamInfo();
        lnbParams.NumberOfAntennas = 1;
        lnbParams.LnbParams = new LnbParams[MAX_LNB_PARAM_COUNT];
        lnbParams.LnbParams[0].AntennaNumber = 0;
        lnbParams.LnbParams[0].IsEast = true;
        lnbParams.LnbParams[0].OrbitalPosition = 160;
        lnbParams.LnbParams[0].LowBandLof = (ushort)(dvbsChannel.LnbType.LowBandFrequency / 1000);
        lnbParams.LnbParams[0].SwitchFrequency = (ushort)(dvbsChannel.LnbType.SwitchFrequency / 1000);
        lnbParams.LnbParams[0].HighBandLof = (ushort)(dvbsChannel.LnbType.HighBandFrequency / 1000);

        Marshal.StructureToPtr(lnbParams, _generalBuffer, true);
        //Dump.DumpBinary(_generalBuffer, LNB_PARAM_INFO_SIZE);

        hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.SetLnbParams,
          _generalBuffer, LNB_PARAM_INFO_SIZE,
          _generalBuffer, LNB_PARAM_INFO_SIZE
        );
        if (hr != (int)HResult.Severity.Success)
        {
          this.LogError("Digital Everywhere: failed to apply LNB settings, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        }

        DvbsMultiplexParams tuneRequest = new DvbsMultiplexParams();
        tuneRequest.Frequency = (uint)dvbsChannel.Frequency;
        tuneRequest.SymbolRate = (uint)dvbsChannel.SymbolRate;
        tuneRequest.Lnb = 0;    // To match the AntennaNumber value above.

        // OnBeforeTune() mixed pilot and roll-off settings into the top four bits of the least significant
        // inner FEC rate byte. It seemms that this custom tuning method doesn't support setting pilot and
        // roll-off in this way, so we throw the bits away.
        BinaryConvolutionCodeRate rate = (BinaryConvolutionCodeRate)((int)dvbsChannel.InnerFecRate & 0xf);
        if (rate == BinaryConvolutionCodeRate.Rate1_2)
        {
          tuneRequest.InnerFecRate = DeFecRate.Rate1_2;
        }
        else if (rate == BinaryConvolutionCodeRate.Rate2_3)
        {
          tuneRequest.InnerFecRate = DeFecRate.Rate2_3;
        }
        else if (rate == BinaryConvolutionCodeRate.Rate3_4)
        {
          tuneRequest.InnerFecRate = DeFecRate.Rate3_4;
        }
        else if (rate == BinaryConvolutionCodeRate.Rate5_6)
        {
          tuneRequest.InnerFecRate = DeFecRate.Rate5_6;
        }
        else if (rate == BinaryConvolutionCodeRate.Rate7_8)
        {
          tuneRequest.InnerFecRate = DeFecRate.Rate7_8;
        }
        else
        {
          tuneRequest.InnerFecRate = DeFecRate.Auto;
        }

        tuneRequest.Polarisation = DePolarisation.Vertical;
        if (dvbsChannel.Polarisation == Polarisation.LinearH || dvbsChannel.Polarisation == Polarisation.CircularL)
        {
          tuneRequest.Polarisation = DePolarisation.Horizontal;
        }

        Marshal.StructureToPtr(tuneRequest, _generalBuffer, true);
        //Dump.DumpBinary(_generalBuffer, DVBS_MULTIPLEX_PARAMS_SIZE);

        hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.SelectMultiplexDvbS,
          _generalBuffer, DVBS_MULTIPLEX_PARAMS_SIZE,
          _generalBuffer, DVBS_MULTIPLEX_PARAMS_SIZE
        );
      }
      else
      {
        DVBTChannel dvbtChannel = channel as DVBTChannel;
        if (dvbtChannel is DVBTChannel && _tunerType == CardType.DvbT)
        {
          DvbtMultiplexParams tuneRequest = new DvbtMultiplexParams();
          tuneRequest.Frequency = (uint)dvbtChannel.Frequency;
          tuneRequest.Bandwidth = DeOfdmBandwidth.Bandwidth8;
          if (dvbtChannel.Bandwidth == 7000)
          {
            tuneRequest.Bandwidth = DeOfdmBandwidth.Bandwidth7;
          }
          else if (dvbtChannel.Bandwidth == 6000)
          {
            tuneRequest.Bandwidth = DeOfdmBandwidth.Bandwidth6;
          }
          tuneRequest.Constellation = DeOfdmConstellation.Auto;
          tuneRequest.CodeRateHp = DeOfdmCodeRate.Auto;
          tuneRequest.CodeRateLp = DeOfdmCodeRate.Auto;
          tuneRequest.GuardInterval = DeOfdmGuardInterval.Auto;
          tuneRequest.TransmissionMode = DeOfdmTransmissionMode.Auto;
          tuneRequest.Hierarchy = DeOfdmHierarchy.Auto;

          Marshal.StructureToPtr(tuneRequest, _generalBuffer, true);
          Dump.DumpBinary(_generalBuffer, DVBT_MULTIPLEX_PARAMS_SIZE);
          hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.SelectMultiplexDvbT,
            _generalBuffer, DVBT_MULTIPLEX_PARAMS_SIZE,
            _generalBuffer, DVBT_MULTIPLEX_PARAMS_SIZE
          );
        }
        else
        {
          this.LogDebug("Digital Everywhere: tuning is not supported for this channel");
          return false;
        }
      }

      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      this.LogError("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IConditionalAccessProvider members

    /// <summary>
    /// Open the conditional access interface. For the interface to be opened successfully it is expected
    /// that any necessary hardware (such as a CI slot) is connected.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully opened, otherwise <c>false</c></returns>
    public bool OpenInterface()
    {
      this.LogDebug("Digital Everywhere: open conditional access interface");

      if (!_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }
      if (_isCaInterfaceOpen)
      {
        this.LogWarn("Digital Everywhere: interface is already open");
        return true;
      }

      _mmiBuffer = Marshal.AllocCoTaskMem(CA_DATA_SIZE);
      _pmtBuffer = Marshal.AllocCoTaskMem(CA_DATA_SIZE);
      _isCamReady = IsInterfaceReady();
      _isCamPresent = _isCamReady;
      if (_isCamReady)
      {
        ReadApplicationInformation();
      }

      _isCaInterfaceOpen = true;
      StartMmiHandlerThread();

      this.LogDebug("Digital Everywhere: result = success");
      return true;
    }

    /// <summary>
    /// Close the conditional access interface.
    /// </summary>
    /// <returns><c>true</c> if the interface is successfully closed, otherwise <c>false</c></returns>
    public bool CloseInterface()
    {
      this.LogDebug("Digital Everywhere: close conditional access interface");

      StopMmiHandlerThread();

      if (_mmiBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_mmiBuffer);
        _mmiBuffer = IntPtr.Zero;
      }
      if (_pmtBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_pmtBuffer);
        _pmtBuffer = IntPtr.Zero;
      }

      _isCamPresent = false;
      _isCamReady = false;
      _isCaInterfaceOpen = false;

      this.LogDebug("Digital Everywhere: result = success");
      return true;
    }

    /// <summary>
    /// Reset the conditional access interface.
    /// </summary>
    /// <param name="resetTuner">This parameter will be set to <c>true</c> if the tuner must be reset
    ///   for the interface to be completely and successfully reset.</param>
    /// <returns><c>true</c> if the interface is successfully reset, otherwise <c>false</c></returns>
    public bool ResetInterface(out bool resetTuner)
    {
      this.LogDebug("Digital Everywhere: reset conditional access interface");

      resetTuner = false;

      if (!_isDigitalEverywhere)
      {
        this.LogDebug("Digital Everywhere: not initialised or interface not supported");
        return false;
      }

      bool success = CloseInterface();

      CaData data = new CaData(DeCiMessageTag.Reset);
      data.DataLength = 1;
      data.Data[0] = (byte)DeResetType.ForcedHardwareReset;

      Marshal.StructureToPtr(data, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, CA_DATA_SIZE);

      int hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.MmiHostToCam,
        _generalBuffer, CA_DATA_SIZE,
        _generalBuffer, CA_DATA_SIZE
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
      }
      else
      {
        this.LogError("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        success = false;
      }
      return success && OpenInterface();
    }

    /// <summary>
    /// Determine whether the conditional access interface is ready to receive commands.
    /// </summary>
    /// <returns><c>true</c> if the interface is ready, otherwise <c>false</c></returns>
    public bool IsInterfaceReady()
    {
      this.LogDebug("Digital Everywhere: is conditional access interface ready");

      if (!_isCaInterfaceOpen)
      {
        this.LogDebug("Digital Everywhere: not initialised or interface not supported");
        return false;
      }

      DeCiState ciState;
      int hr = GetCiStatus(out ciState);
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("Digital Everywhere: failed to get CI status, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      this.LogDebug("Digital Everywhere: CI state = {0}", ciState.ToString());
      bool isCamReady = false;
      if (ciState.HasFlag(DeCiState.CamPresent | DeCiState.CamIsDvb | DeCiState.CamReady | DeCiState.ApplicationInfoAvailable) &&
        !ciState.HasFlag(DeCiState.CamError))
      {
        isCamReady = true;
      }
      this.LogDebug("Digital Everywhere: result = {0}", isCamReady);
      return isCamReady;
    }

    /// <summary>
    /// Send a command to to the conditional access interface.
    /// </summary>
    /// <param name="channel">The channel information associated with the service which the command relates to.</param>
    /// <param name="listAction">It is assumed that the interface may be able to decrypt one or more services
    ///   simultaneously. This parameter gives the interface an indication of the number of services that it
    ///   will be expected to manage.</param>
    /// <param name="command">The type of command.</param>
    /// <param name="pmt">The program map table for the service.</param>
    /// <param name="cat">The conditional access table for the service.</param>
    /// <returns><c>true</c> if the command is successfully sent, otherwise <c>false</c></returns>
    public bool SendCommand(IChannel channel, CaPmtListManagementAction listAction, CaPmtCommand command, Pmt pmt, Cat cat)
    {
      this.LogDebug("Digital Everywhere: send conditional access command, list action = {0}, command = {1}", listAction, command);

      if (!_isCaInterfaceOpen)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }
      if (!_isCamReady)
      {
        this.LogError("Digital Everywhere: the CAM is not ready");
        return false;
      }
      if (pmt == null)
      {
        this.LogError("Digital Everywhere: PMT not supplied");
        return true;
      }

      ReadOnlyCollection<byte> rawPmt = pmt.GetRawPmt();
      if (rawPmt.Count > MAX_PMT_LENGTH - 2)
      {
        this.LogError("Digital Everywhere: buffer capacity too small");
        return false;
      }

      CaData data = new CaData(DeCiMessageTag.Pmt);
      data.DataLength = (ushort)(rawPmt.Count + 2);
      data.Data[0] = (byte)listAction;
      data.Data[1] = (byte)command;

      rawPmt.CopyTo(data.Data, 2);

      Marshal.StructureToPtr(data, _pmtBuffer, true);
      //Dump.DumpBinary(_pmtBuffer, CA_DATA_SIZE);

      int hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.MmiHostToCam,
        _pmtBuffer, CA_DATA_SIZE,
        _pmtBuffer, CA_DATA_SIZE
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      // Failure indicates a Firewire communication problem.
      // Success does *not* indicate that the service will be descrambled.
      this.LogError("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IConditionalAccessMenuActions members

    /// <summary>
    /// Set the menu call back delegate.
    /// </summary>
    /// <param name="callBacks">The call back delegate.</param>
    public void SetCallBacks(IConditionalAccessMenuCallBacks callBacks)
    {
      lock (_caMenuCallBackLock)
      {
        _caMenuCallBacks = callBacks;
      }
      StartMmiHandlerThread();
    }

    /// <summary>
    /// Send a request from the user to the CAM to open the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool EnterMenu()
    {
      this.LogDebug("Digital Everywhere: enter menu");
      CaData data = new CaData(DeCiMessageTag.EnterMenu);
      return SendMmi(data);
    }

    /// <summary>
    /// Send a request from the user to the CAM to close the menu.
    /// </summary>
    /// <returns><c>true</c> if the request is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool CloseMenu()
    {
      this.LogDebug("Digital Everywhere: close menu");
      CaData data = new CaData(DeCiMessageTag.Mmi);
      byte[] apdu = DvbMmiHandler.CreateMmiClose(0);
      data.DataLength = (ushort)apdu.Length;
      Buffer.BlockCopy(apdu, 0, data.Data, 0, apdu.Length);
      return SendMmi(data);
    }

    /// <summary>
    /// Send a menu entry selection from the user to the CAM.
    /// </summary>
    /// <param name="choice">The index of the selection as an unsigned byte value.</param>
    /// <returns><c>true</c> if the selection is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool SelectMenuEntry(byte choice)
    {
      this.LogDebug("Digital Everywhere: select menu entry, choice = {0}", choice);
      CaData data = new CaData(DeCiMessageTag.Mmi);
      byte[] apdu = DvbMmiHandler.CreateMmiMenuAnswer(choice);
      data.DataLength = (ushort)apdu.Length;
      Buffer.BlockCopy(apdu, 0, data.Data, 0, apdu.Length);
      return SendMmi(data);
    }

    /// <summary>
    /// Send an answer to an enquiry from the user to the CAM.
    /// </summary>
    /// <param name="cancel"><c>True</c> to cancel the enquiry.</param>
    /// <param name="answer">The user's answer to the enquiry.</param>
    /// <returns><c>true</c> if the answer is successfully passed to and processed by the CAM, otherwise <c>false</c></returns>
    public bool AnswerEnquiry(bool cancel, string answer)
    {
      if (answer == null)
      {
        answer = string.Empty;
      }
      this.LogDebug("Digital Everywhere: answer enquiry, answer = {0}, cancel = {1}", answer, cancel);

      CaData data = new CaData(DeCiMessageTag.Mmi);
      MmiResponseType responseType = MmiResponseType.Answer;
      if (cancel)
      {
        responseType = MmiResponseType.Cancel;
      }
      byte[] apdu = DvbMmiHandler.CreateMmiEnquiryAnswer(responseType, answer);
      data.DataLength = (ushort)apdu.Length;
      Buffer.BlockCopy(apdu, 0, data.Data, 0, apdu.Length);
      return SendMmi(data);
    }

    #endregion

    #region IDiseqcDevice members

    /// <summary>
    /// Send a tone/data burst command, and then set the 22 kHz continuous tone state.
    /// </summary>
    /// <param name="toneBurstState">The tone/data burst command to send, if any.</param>
    /// <param name="tone22kState">The 22 kHz continuous tone state to set.</param>
    /// <returns><c>true</c> if the tone state is set successfully, otherwise <c>false</c></returns>
    public bool SetToneState(ToneBurst toneBurstState, Tone22k tone22kState)
    {
      // TODO: this function needs to be tested. I'm uncertain whether
      // the driver will accept commands with no DiSEqC messages.
      this.LogDebug("Digital Everywhere: set tone state, burst = {0}, 22 kHz = {1}", toneBurstState, tone22kState);

      if (!_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }

      LnbCommand lnbCommand = new LnbCommand();
      lnbCommand.Voltage = 0xff;
      lnbCommand.Tone22k = De22k.Off;
      if (tone22kState == Tone22k.On)
      {
        lnbCommand.Tone22k = De22k.On;
      }
      lnbCommand.ToneBurst = DeToneBurst.Undefined;
      if (toneBurstState == ToneBurst.ToneBurst)
      {
        lnbCommand.ToneBurst = DeToneBurst.ToneBurst;
      }
      else if (toneBurstState == ToneBurst.DataBurst)
      {
        lnbCommand.ToneBurst = DeToneBurst.DataBurst;
      }
      lnbCommand.NumberOfMessages = 0;

      Marshal.StructureToPtr(lnbCommand, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, LNB_COMMAND_SIZE);

      int hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.LnbControl,
        _generalBuffer, LNB_COMMAND_SIZE,
        _generalBuffer, LNB_COMMAND_SIZE
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      this.LogError("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Send an arbitrary DiSEqC command.
    /// </summary>
    /// <param name="command">The command to send.</param>
    /// <returns><c>true</c> if the command is sent successfully, otherwise <c>false</c></returns>
    public bool SendCommand(byte[] command)
    {
      this.LogDebug("Digital Everywhere: send DiSEqC command");

      if (!_isDigitalEverywhere)
      {
        this.LogWarn("Digital Everywhere: not initialised or interface not supported");
        return false;
      }
      if (command == null || command.Length == 0)
      {
        this.LogError("Digital Everywhere: command not supplied");
        return true;
      }
      if (command.Length > MAX_DISEQC_MESSAGE_LENGTH)
      {
        this.LogError("Digital Everywhere: command too long, length = {0}", command.Length);
        return false;
      }

      LnbCommand lnbCommand = new LnbCommand();
      lnbCommand.Voltage = 0xff;
      lnbCommand.Tone22k = De22k.Undefined;
      lnbCommand.ToneBurst = DeToneBurst.Undefined;
      lnbCommand.NumberOfMessages = 1;
      lnbCommand.DiseqcMessages = new DiseqcMessage[MAX_DISEQC_MESSAGE_COUNT];
      lnbCommand.DiseqcMessages[0].MessageLength = (byte)command.Length;
      lnbCommand.DiseqcMessages[0].Message = new byte[MAX_DISEQC_MESSAGE_LENGTH];
      Buffer.BlockCopy(command, 0, lnbCommand.DiseqcMessages[0].Message, 0, command.Length);

      Marshal.StructureToPtr(lnbCommand, _generalBuffer, true);
      //Dump.DumpBinary(_generalBuffer, LNB_COMMAND_SIZE);

      int hr = _propertySet.Set(BDA_EXTENSION_PROPERTY_SET, (int)BdaExtensionProperty.LnbControl,
        _generalBuffer, LNB_COMMAND_SIZE,
        _generalBuffer, LNB_COMMAND_SIZE
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Digital Everywhere: result = success");
        return true;
      }

      this.LogError("Digital Everywhere: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    /// <summary>
    /// Retrieve the response to a previously sent DiSEqC command (or alternatively, check for a command
    /// intended for this tuner).
    /// </summary>
    /// <param name="response">The response (or command).</param>
    /// <returns><c>true</c> if the response is read successfully, otherwise <c>false</c></returns>
    public bool ReadResponse(out byte[] response)
    {
      // Not supported.
      response = null;
      return false;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      CloseInterface();
      _propertySet = null;
      if (_generalBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_generalBuffer);
        _generalBuffer = IntPtr.Zero;
      }
      _isDigitalEverywhere = false;
    }

    #endregion
  }
}