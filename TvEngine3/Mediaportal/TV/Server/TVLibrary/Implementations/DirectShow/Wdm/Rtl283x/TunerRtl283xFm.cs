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
using System.Runtime.InteropServices;
using System.Threading;
using DirectShowLib;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow.Wdm.Analog.Component;
using Mediaportal.TV.Server.TVLibrary.Implementations.Helper;
using Mediaportal.TV.Server.TVLibrary.Implementations.Mpeg2Ts;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Analyzer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Microsoft.Win32;
using BroadcastStandard = Mediaportal.TV.Server.TVLibrary.Interfaces.Analyzer.BroadcastStandard;
using Encoder = Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow.Wdm.Analog.Component.Encoder;

namespace Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow.Wdm.Rtl283x
{
  internal class TunerRtl283xFm : TunerDirectShowBase
  {
    #region enums

    // Any interaction with the graph or tuner must be done from a COM single
    // threaded apartment. We run in a multi-threaded apartment, so we need to
    // perform the following interactions from an STA thread.
    private enum GraphJobType
    {
      Load,
      Tune,
      UpdateSignalStatus,
      SetGraphState,
      Unload,
      TsWriterSubChannelMethod,
      TsWriterScanMethod
    }

    private enum Rtl283xFmResult
    {
      Fail = 0,
      Success,
    }

    private enum Rtl283xFmScanStepSize : int
    {
      Step50kHz = 50,
      Step100kHz = 100,
      Step200kHz = 200
    }

    private enum Rtl283xFmScanDirection
    {
      Decrease = 0,
      Increase
    }

    private enum Rtl283xFmSampleRate : uint
    {
      Sr32000Hz = 32000,
      Sr48000Hz = 48000
    }

    private enum Rtl283xFmDeEmphasisTimeConstant : byte
    {
      FiftyMicroSeconds = 0,
      SeventyFiveMicroSeconds
    }

    [Flags]
    private enum Rtl283xFmProperty : byte
    {
      // Attenuate the audio output level (audio and noise) if signal quality is poor. Default: on.
      SoftMute = 0x01,
      // Blend stereo audio gradually based on the signal quality. Default: on.
      StereoBlend = 0x02,
      // Only go to stereo mode when the channel separation is more than 10 dB. Default: off.
      StereoSwitch = 0x04,
      // Reduce audio bandwidth in low SNR conditions. Default: on.
      HighCutControl = 0x08
    }

    #endregion

    #region COM interfaces

    [Guid("6c433cea-7f9c-40cc-a670-bacae16097b8"),
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IRtl283xFmSource
    {
      /// <summary>
      /// Debug use only.
      /// </summary>
      /// <param name="mediaType">The media type to set.</param>
      /// <returns>an HRESULT indicating whether the media type was set successfully</returns>
      [PreserveSig]
      int DABSetMediaType(byte mediaType);

      /// <summary>
      /// Call this function to set the audio sample rate.
      /// </summary>
      /// <param name="sampleRate">The audio sample rate for the tuner to use.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult SetAudioSampleRate(Rtl283xFmSampleRate sampleRate);

      /// <summary>
      /// Call this function to set the tuner frequency directly.
      /// </summary>
      /// <remarks>
      /// The tuner will switch to direct-tuning mode.
      /// </remarks>
      /// <param name="frequency">The frequency to tune to, in kHz.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult SetFrequency(int frequency);

      /// <summary>
      /// Call this function to perform an automated scan for the next available
      /// station.
      /// </summary>
      /// <remarks>
      /// The tuner will switch to scan-tuning mode.
      /// </remarks>
      /// <param name="startFrequency">The frequency to start scanning from, in kHz.</param>
      /// <param name="stepSize">The scan step size.</param>
      /// <param name="direction">The direction for the scan search relative to the start frequency.</param>
      /// <param name="maxSteps">The maximum number of frequencies to check.</param>
      /// <param name="stationFrequency">The frequency of the station found by the scan.</param>
      /// <param name="stationSignalQuality">The signal quality of the station found by the scan (0 - 100, best is 100).</param>
      /// <returns><c>RtlFmResult.Success</c> if a station was found, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult ScanNextProg(int startFrequency, Rtl283xFmScanStepSize stepSize, Rtl283xFmScanDirection direction, int maxSteps, out int stationFrequency, out int stationSignalQuality);

      /// <summary>
      /// Call this function to obtain the frequency range that the tuner supports.
      /// </summary>
      /// <remarks>
      /// Doesn't work. The values returned are always zero.
      /// </remarks>
      /// <param name="lowerLimit">The lower limit of the tuner's range, in kHz.</param>
      /// <param name="upperLimit">The upper limit of the tuner's range, in kHz.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult GetTunerRange(out int lowerLimit, out int upperLimit);

      /// <summary>
      /// Call this function to check whether tuner is locked onto a signal.
      /// </summary>
      /// <remarks>
      /// Only available in direct-tuning mode.
      /// </remarks>
      /// <param name="isLocked"><c>True</c> if the tuner is currently locked.</param>
      /// <returns><c>RtlFmResult.Success</c> if the lock status is successfully retrieved, otherwise <c>RtlFmResult.Fail</c>
      /// if the status is not successfully retrieved or the device is not in direct-tuning mode.</returns>
      [PreserveSig]
      Rtl283xFmResult GetSignalLock([Out, MarshalAs(UnmanagedType.Bool)] out bool isLocked);

      /// <summary>
      /// Call this function to check the current signal quality.
      /// </summary>
      /// <remarks>
      /// Only available in direct-tuning mode.
      /// </remarks>
      /// <param name="signalQuality">The current quality of the signal that the
      /// tuner is tuned to.
      /// 0   = no signal, tuner not locked
      /// 20  = poor, possibly very noisy
      /// 40  = average, tollerable audio quality
      /// 60  = good
      /// >80 = excellent
      /// 100 = maximum
      /// </param>
      /// <returns><c>RtlFmResult.Success</c> if the signal quality is
      /// successfully retrieved, otherwise <c>RtlFmResult.Fail</c> if the
      /// quality is not successfully retrieved or the device is not in
      /// direct-tuning mode.</returns>
      [PreserveSig]
      Rtl283xFmResult GetSignalQuality(out int signalQuality);

      /// <summary>
      /// Call this function to obtain the current audio characteristics.
      /// </summary>
      /// <param name="channelCount">The number of audio channels received.</param>
      /// <param name="sampleRate">The audio sample rate, in Hz.</param>
      /// <param name="sampleSize">The number of bits per sample.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult GetPCMInfo(out byte channelCount, out Rtl283xFmSampleRate sampleRate, out uint sampleSize);

      /// <summary>
      /// Call this function to set the audio de-emphasis time constant.
      /// </summary>
      /// <param name="timeConstant">The de-emphasis time constant.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult SetDeemphasisTC(Rtl283xFmDeEmphasisTimeConstant timeConstant);

      /// <summary>
      /// Call this function to get the values of the signal control properties.
      /// </summary>
      /// <param name="propertyValues">The current property values.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult GetSignalQualityCtr(out Rtl283xFmProperty propertyValues);

      /// <summary>
      /// Call this function to set the values of the signal control properties.
      /// </summary>
      /// <param name="propertyValues">The property values to set.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult SetSignalQualityCtr(Rtl283xFmProperty propertyValues);

      /// <summary>
      /// Call this function to set the quality threshold for station
      /// identification during scanning.
      /// </summary>
      /// <param name="thresholdQuality">The quality threshold value. Value should be between 10 and 100.</param>
      /// <returns><c>RtlFmResult.Success</c> if successful, otherwise <c>RtlFmResult.Fail</c></returns>
      [PreserveSig]
      Rtl283xFmResult SetScanStopQuality(uint thresholdQuality);
    }

    #endregion

    #region graph delegation/wrapping

    private class GraphJob
    {
      public GraphJobType JobType;
      public object[] Parameters;
      public object ReturnValue;
      public Exception ThrownException;
      public AutoResetEvent WaitEvent;
    }

    // A wrapper class for TsWriter. This class makes it easy to funnel all
    // TsWriter interaction through our STA thread.
    private class TsWriterStaWrapper : ITsFilter, ITsChannelScan
    {
      private TsWriterSubChannelJob _delegateSubChannel = null;
      private TsWriterScanJob _delegateScan = null;

      public TsWriterStaWrapper(TsWriterSubChannelJob delegateSubChannel, TsWriterScanJob delegateScan)
      {
        _delegateSubChannel = delegateSubChannel;
        _delegateScan = delegateScan;
      }

      #region sub-channel delegation

      public int AddChannel(ref int handle)
      {
        object[] parameters = new object[2] { "AddChannel", handle };
        int hr = _delegateSubChannel(ref parameters);
        handle = (int)parameters[1];
        return hr;
      }

      public int DeleteChannel(int handle)
      {
        object[] parameters = new object[2] { "DeleteChannel", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int DeleteAllChannels()
      {
        object[] parameters = new object[1] { "DeleteAllChannels" };
        return _delegateSubChannel(ref parameters);
      }

      public int AnalyserAddPid(int handle, int pid)
      {
        object[] parameters = new object[3] { "AnalyserAddPid", handle, pid };
        return _delegateSubChannel(ref parameters);
      }

      public int AnalyserRemovePid(int handle, int pid)
      {
        object[] parameters = new object[3] { "AnalyserRemovePid", handle, pid };
        return _delegateSubChannel(ref parameters);
      }

      public int AnalyserGetPidCount(int handle, out int pidCount)
      {
        pidCount = 0;
        object[] parameters = new object[3] { "AnalyserGetPidCount", handle, pidCount };
        int hr = _delegateSubChannel(ref parameters);
        pidCount = (int)parameters[2];
        return hr;
      }

      public int AnalyserGetPid(int handle, int pidIndex, out int pid, out EncryptionState encryptionState)
      {
        pid = 0;
        encryptionState = EncryptionState.NotSet;
        object[] parameters = new object[5] { "AnalyserGetPid", handle, pidIndex, pid, encryptionState };
        int hr = _delegateSubChannel(ref parameters);
        pid = (int)parameters[3];
        encryptionState = (EncryptionState)parameters[4];
        return hr;
      }

      public int AnalyserSetCallBack(int handle, IEncryptionStateChangeCallBack callBack)
      {
        object[] parameters = new object[3] { "AnalyserSetCallBack", handle, callBack };
        return _delegateSubChannel(ref parameters);
      }

      public int AnalyserReset(int handle)
      {
        object[] parameters = new object[2] { "AnalyserReset", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int PmtSetPmtPid(int handle, int pmtPid, int serviceId)
      {
        object[] parameters = new object[4] { "PmtSetPmtPid", handle, pmtPid, serviceId };
        return _delegateSubChannel(ref parameters);
      }

      public int PmtSetCallBack(int handle, IPmtCallBack callBack)
      {
        object[] parameters = new object[3] { "PmtSetCallBack", handle, callBack };
        return _delegateSubChannel(ref parameters);
      }

      public int PmtGetPmtData(int handle, IntPtr pmtData)
      {
        object[] parameters = new object[3] { "PmtGetPmtData", handle, pmtData };
        return _delegateSubChannel(ref parameters);
      }

      public int RecordSetRecordingFileNameW(int handle, string fileName)
      {
        object[] parameters = new object[3] { "RecordSetRecordingFileNameW", handle, fileName };
        return _delegateSubChannel(ref parameters);
      }

      public int RecordStartRecord(int handle)
      {
        object[] parameters = new object[2] { "RecordStartRecord", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int RecordStopRecord(int handle)
      {
        object[] parameters = new object[2] { "RecordStopRecord", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int RecordSetPmtPid(int handle, int pmtPid, int serviceId, byte[] pmtData, int pmtLength)
      {
        object[] parameters = new object[6] { "RecordSetPmtPid", handle, pmtPid, serviceId, pmtData, pmtLength };
        return _delegateSubChannel(ref parameters);
      }

      public int RecorderSetVideoAudioObserver(int handle, IVideoAudioObserver observer)
      {
        object[] parameters = new object[3] { "RecorderSetVideoAudioObserver", handle, observer };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftSetTimeShiftingFileNameW(int handle, string fileName)
      {
        object[] parameters = new object[3] { "TimeShiftSetTimeShiftingFileNameW", handle, fileName };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftStart(int handle)
      {
        object[] parameters = new object[2] { "TimeShiftStart", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftStop(int handle)
      {
        object[] parameters = new object[2] { "TimeShiftStop", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftReset(int handle)
      {
        object[] parameters = new object[2] { "TimeShiftReset", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftGetBufferSize(int handle, out long size)
      {
        size = 0;
        object[] parameters = new object[3] { "TimeShiftGetBufferSize", handle, size };
        int hr = _delegateSubChannel(ref parameters);
        size = (long)parameters[2];
        return hr;
      }

      public int TimeShiftSetPmtPid(int handle, int pmtPid, int serviceId, byte[] pmtData, int pmtLength)
      {
        object[] parameters = new object[6] { "TimeShiftSetPmtPid", handle, pmtPid, serviceId, pmtData, pmtLength };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftPause(int handle, byte onOff)
      {
        object[] parameters = new object[3] { "TimeShiftPause", handle, onOff };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftSetParams(int handle, int minFiles, int maxFiles, uint chunkSize)
      {
        object[] parameters = new object[5] { "TimeShiftSetParams", handle, minFiles, maxFiles, chunkSize };
        return _delegateSubChannel(ref parameters);
      }

      public int TimeShiftGetCurrentFilePosition(int handle, out long position, out long bufferId)
      {
        position = 0;
        bufferId = 0;
        object[] parameters = new object[4] { "TimeShiftGetCurrentFilePosition", handle, position, bufferId };
        int hr =  _delegateSubChannel(ref parameters);
        position = (long)parameters[2];
        bufferId = (long)parameters[3];
        return hr;
      }

      public int SetVideoAudioObserver(int handle, IVideoAudioObserver observer)
      {
        object[] parameters = new object[3] { "SetVideoAudioObserver", handle, observer };
        return _delegateSubChannel(ref parameters);
      }

      public int TTxStart(int handle)
      {
        object[] parameters = new object[2] { "TTxStart", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int TTxStop(int handle)
      {
        object[] parameters = new object[2] { "TTxStop", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int TTxSetTeletextPid(int handle, int teletextPid)
      {
        object[] parameters = new object[3] { "TTxSetTeletextPid", handle, teletextPid };
        return _delegateSubChannel(ref parameters);
      }

      public int CaReset(int handle)
      {
        object[] parameters = new object[2] { "CaReset", handle };
        return _delegateSubChannel(ref parameters);
      }

      public int CaSetCallBack(int handle, ICaCallBack callBack)
      {
        object[] parameters = new object[3] { "CaSetCallBack", handle, callBack };
        return _delegateSubChannel(ref parameters);
      }

      public int CaGetCaData(int handle, IntPtr caData)
      {
        object[] parameters = new object[3] { "CaGetCaData", handle, caData };
        return _delegateSubChannel(ref parameters);
      }

      public int GetStreamQualityCounters(int handle, out int timeShiftByteCount, out int recordByteCount, out int timeShiftDiscontinuityCount, out int recordDiscontinuityCount)
      {
        timeShiftByteCount = 0;
        recordByteCount = 0;
        timeShiftDiscontinuityCount = 0;
        recordDiscontinuityCount = 0;
        object[] parameters = new object[6] { "GetStreamQualityCounters", handle, timeShiftByteCount, recordByteCount, timeShiftDiscontinuityCount, recordDiscontinuityCount };
        int hr = _delegateSubChannel(ref parameters);
        timeShiftByteCount = (int)parameters[2];
        recordByteCount = (int)parameters[3];
        timeShiftDiscontinuityCount = (int)parameters[4];
        recordDiscontinuityCount = (int)parameters[5];
        return hr;
      }

      public int TimeShiftSetChannelType(int handle, int channelType)
      {
        object[] parameters = new object[3] { "TimeShiftSetChannelType", handle, channelType };
        return _delegateSubChannel(ref parameters);
      }

      #endregion

      #region scan delegation

      public int SetCallBack(IChannelScanCallBack callBack)
      {
        object[] parameters = new object[2] { "SetCallBack", callBack };
        return _delegateScan(ref parameters);
      }

      public int ScanStream(BroadcastStandard broadcastStandard)
      {
        object[] parameters = new object[2] { "ScanStream", broadcastStandard };
        return _delegateScan(ref parameters);
      }

      public int StopStreamScan()
      {
        object[] parameters = new object[1] { "StopStreamScan" };
        return _delegateScan(ref parameters);
      }

      public int GetServiceCount(out int serviceCount)
      {
        serviceCount = 0;
        object[] parameters = new object[2] { "GetServiceCount", serviceCount };
        int hr = _delegateScan(ref parameters);
        serviceCount = (int)parameters[1];
        return hr;
      }

      public int GetServiceDetail(int index,
                          out int originalNetworkId,
                          out int transportStreamId,
                          out int serviceId,
                          out IntPtr serviceName,
                          out IntPtr providerName,
                          out IntPtr logicalChannelNumber,
                          out int serviceType,
                          out int videoStreamCount,
                          out int audioStreamCount,
                          out bool isHighDefinition,
                          out bool isEncrypted,
                          out bool isRunning,
                          out int pmtPid,
                          out int previousOriginalNetworkId,
                          out int previousTransportStreamId,
                          out int previousServiceId,
                          out int networkIdCount,
                          out IntPtr networkIds,
                          out int bouquetIdCount,
                          out IntPtr bouquetIds,
                          out int languageCount,
                          out IntPtr languages,
                          out int availableInCellCount,
                          out IntPtr availableInCells,
                          out int unavailableInCellCount,
                          out IntPtr unavailableInCells,
                          out int targetRegionCount,
                          out IntPtr targetRegions,
                          out int availableInCountryCount,
                          out IntPtr availableInCountries,
                          out int unavailableInCountryCount,
                          out IntPtr unavailableInCountries)
      {
        originalNetworkId = 0;
        transportStreamId = 0;
        serviceId = 0;
        serviceName = IntPtr.Zero;
        providerName = IntPtr.Zero;
        logicalChannelNumber = IntPtr.Zero;
        serviceType = 0;
        videoStreamCount = 0;
        audioStreamCount = 0;
        isHighDefinition = false;
        isEncrypted = false;
        isRunning = false;
        pmtPid = 0;
        previousOriginalNetworkId = 0;
        previousTransportStreamId = 0;
        previousServiceId = 0;
        networkIdCount = 0;
        networkIds = IntPtr.Zero;
        bouquetIdCount = 0;
        bouquetIds = IntPtr.Zero;
        languageCount = 0;
        languages = IntPtr.Zero;
        availableInCellCount = 0;
        availableInCells = IntPtr.Zero;
        unavailableInCellCount = 0;
        unavailableInCells = IntPtr.Zero;
        targetRegionCount = 0;
        targetRegions = IntPtr.Zero;
        availableInCountryCount = 0;
        availableInCountries = IntPtr.Zero;
        unavailableInCountryCount = 0;
        unavailableInCountries = IntPtr.Zero;
        object[] parameters = new object[34] { "GetServiceDetail", index, originalNetworkId,
                                              transportStreamId, serviceId, serviceName,
                                              providerName, logicalChannelNumber, serviceType,
                                              videoStreamCount, audioStreamCount, isHighDefinition,
                                              isEncrypted, isRunning, pmtPid,
                                              previousOriginalNetworkId, previousTransportStreamId,
                                              previousServiceId, networkIdCount, networkIds,
                                              bouquetIdCount, bouquetIds, languageCount, languages,
                                              availableInCellCount, availableInCells,
                                              unavailableInCellCount, unavailableInCells,
                                              targetRegionCount, targetRegions,
                                              availableInCountryCount, availableInCountries,
                                              unavailableInCountryCount, unavailableInCountries };
        int hr = _delegateScan(ref parameters);
        originalNetworkId = (int)parameters[2];
        transportStreamId = (int)parameters[3];
        serviceId = (int)parameters[4];
        serviceName = (IntPtr)parameters[5];
        providerName = (IntPtr)parameters[6];
        logicalChannelNumber = (IntPtr)parameters[7];
        serviceType = (int)parameters[8];
        videoStreamCount = (int)parameters[9];
        audioStreamCount = (int)parameters[10];
        isHighDefinition = (bool)parameters[11];
        isEncrypted = (bool)parameters[12];
        isRunning = (bool)parameters[13];
        pmtPid = (int)parameters[14];
        previousOriginalNetworkId = (int)parameters[15];
        previousTransportStreamId = (int)parameters[16];
        previousServiceId = (int)parameters[17];
        networkIdCount = (int)parameters[18];
        networkIds = (IntPtr)parameters[19];
        bouquetIdCount = (int)parameters[20];
        bouquetIds = (IntPtr)parameters[21];
        languageCount = (int)parameters[22];
        languages = (IntPtr)parameters[23];
        availableInCellCount = (int)parameters[24];
        availableInCells = (IntPtr)parameters[25];
        unavailableInCellCount = (int)parameters[26];
        unavailableInCells = (IntPtr)parameters[27];
        targetRegionCount = (int)parameters[28];
        targetRegions = (IntPtr)parameters[29];
        availableInCountryCount = (int)parameters[30];
        availableInCountries = (IntPtr)parameters[31];
        unavailableInCountryCount = (int)parameters[32];
        unavailableInCountries = (IntPtr)parameters[33];
        return hr;
      }

      public int ScanNetwork()
      {
        object[] parameters = new object[1] { "ScanNetwork" };
        return _delegateScan(ref parameters);
      }

      public int StopNetworkScan(out bool isOtherMuxServiceInfoAvailable)
      {
        isOtherMuxServiceInfoAvailable = false;
        object[] parameters = new object[2] { "StopNetworkScan", isOtherMuxServiceInfoAvailable };
        int hr = _delegateScan(ref parameters);
        isOtherMuxServiceInfoAvailable = (bool)parameters[1];
        return hr;
      }

      public int GetMultiplexCount(out int multiplexCount)
      {
        multiplexCount = 0;
        object[] parameters = new object[2] { "GetMultiplexCount", multiplexCount };
        int hr = _delegateScan(ref parameters);
        multiplexCount = (int)parameters[1];
        return hr;
      }

      public int GetMultiplexDetail(int index,
                              out int originalNetworkId,
                              out int transportStreamId,
                              out int type,
                              out int frequency,
                              out int polarisation,
                              out int modulation,
                              out int symbolRate,
                              out int bandwidth,
                              out int innerFecRate,
                              out int rollOff,
                              out int longitude,
                              out int cellId,
                              out int cellIdExtension,
                              out int plpId)
      {
        originalNetworkId = 0;
        transportStreamId = 0;
        type = 0;
        frequency = 0;
        polarisation = 0;
        modulation = 0;
        symbolRate = 0;
        bandwidth = 0;
        innerFecRate = 0;
        rollOff = 0;
        longitude = 0;
        cellId = 0;
        cellIdExtension = 0;
        plpId = 0;
        object[] parameters = new object[16] { "GetMultiplexDetail", index, originalNetworkId,
                                              transportStreamId, type, frequency, polarisation,
                                              modulation, symbolRate, bandwidth, innerFecRate,
                                              rollOff, longitude, cellId, cellIdExtension, plpId };
        int hr = _delegateScan(ref parameters);
        originalNetworkId = (int)parameters[1];
        transportStreamId = (int)parameters[2];
        type = (int)parameters[3];
        frequency = (int)parameters[4];
        polarisation = (int)parameters[5];
        modulation = (int)parameters[6];
        symbolRate = (int)parameters[7];
        bandwidth = (int)parameters[8];
        innerFecRate = (int)parameters[9];
        rollOff = (int)parameters[10];
        longitude = (int)parameters[11];
        cellId = (int)parameters[12];
        cellIdExtension = (int)parameters[13];
        plpId = (int)parameters[14];
        return hr;
      }

      public int GetTargetRegionName(long targetRegionId, out IntPtr name)
      {
        name = IntPtr.Zero;
        object[] parameters = new object[3] { "GetTargetRegionName", targetRegionId, name };
        int hr = _delegateScan(ref parameters);
        name = (IntPtr)parameters[2];
        return hr;
      }

      public int GetBouquetName(int bouquetId, out IntPtr name)
      {
        name = IntPtr.Zero;
        object[] parameters = new object[3] { "GetBouquetName", bouquetId, name };
        int hr = _delegateScan(ref parameters);
        name = (IntPtr)parameters[2];
        return hr;
      }

      public int GetNetworkName(int networkId, out IntPtr name)
      {
        name = IntPtr.Zero;
        object[] parameters = new object[3] { "GetNetworkName", networkId, name };
        int hr = _delegateScan(ref parameters);
        name = (IntPtr)parameters[2];
        return hr;
      }

      #endregion
    }

    #endregion

    #region delegates

    private delegate int TsWriterSubChannelJob(ref object[] parameters);
    private delegate int TsWriterScanJob(ref object[] parameters);

    #endregion

    #region constants

    private static readonly Guid SOURCE_FILTER_CLSID = new Guid(0x6b368f8c, 0xf383, 0x44d3, 0xb8, 0xc2, 0x3a, 0x15, 0x0b, 0x70, 0xb1, 0xc9);

    #endregion

    #region variables

    private IRtl283xFmSource _fmSource = null;
    private IBaseFilter _filterSource = null;
    private Encoder _encoder = null;
    private DsDevice _mainTunerDevice = null;
    private bool _mainTunerDeviceInUse = false;
    private TsWriterStaWrapper _staTsWriter = null;

    // STA graph thread variables.
    private object _graphThreadLock = new object();
    private Thread _graphThread = null;
    private volatile bool _stopGraphThread = false;
    private AutoResetEvent _graphThreadWaitEvent = null;

    private object _jobQueueLock = new object();
    private Queue<GraphJob> _jobs = new Queue<GraphJob>();

    #endregion

    /// <summary>
    /// Initialise a new instance of the <see cref="TunerRtl283xFm"/> class.
    /// </summary>
    /// <param name="mainTunerDevice">The main BDA tuner device for for the tuner.</param>
    public TunerRtl283xFm(DsDevice mainTunerDevice)
      : base("Realtek RTL283x FM Tuner", mainTunerDevice.DevicePath + "FM", CardType.Analog)
    {
      _mainTunerDevice = mainTunerDevice;
      SetProductAndTunerInstanceIds(_mainTunerDevice);
    }

    /// <summary>
    /// Reload the tuner's configuration.
    /// </summary>
    public override void ReloadConfiguration()
    {
      base.ReloadConfiguration();

      if (_encoder != null)
      {
        _encoder.ReloadConfiguration(_tunerId);
      }
    }

    /// <summary>
    /// Allocate a new sub-channel instance.
    /// </summary>
    /// <param name="id">The identifier for the sub-channel.</param>
    /// <returns>the new sub-channel instance</returns>
    public override ITvSubChannel CreateNewSubChannel(int id)
    {
      return new SubChannelMpeg2Ts(id, _staTsWriter);
    }

    /// <summary>
    /// Actually update tuner signal status statistics.
    /// </summary>
    /// <param name="onlyUpdateLock"><c>True</c> to only update lock status.</param>
    private void InternalPerformSignalStatusUpdate(bool onlyUpdateLock)
    {
      if (_fmSource == null)
      {
        _isSignalPresent = false;
        _isSignalLocked = false;
        _signalLevel = 0;
        _signalQuality = 0;
        return;
      }

      bool isSignalLocked;
      if (_fmSource.GetSignalLock(out isSignalLocked) == Rtl283xFmResult.Fail)
      {
        this.LogWarn("RTL283x FM: failed to update signal lock status");
        isSignalLocked = false;
      }
      _isSignalLocked = isSignalLocked;
      if (onlyUpdateLock)
      {
        return;
      }
      _isSignalPresent = _isSignalLocked;
      if (_fmSource.GetSignalQuality(out _signalQuality) == Rtl283xFmResult.Fail)
      {
        this.LogWarn("RTL283x FM: failed to update signal quality");
        _signalQuality = 0;
      }
      _signalLevel = _signalQuality;
    }

    #region graph thread

    /// <summary>
    /// Do something that requires interaction with the graph or tuner.
    /// </summary>
    private object InvokeGraphJob(GraphJobType jobType, ref object[] parameters)
    {
      lock (_graphThreadLock)
      {
        // Kill the existing thread if it is in "zombie" state.
        if (_graphThread != null && !_graphThread.IsAlive)
        {
          StopGraphThread();
        }

        if (_graphThread == null)
        {
          this.LogDebug("RTL283x FM: starting new graph thread");
          lock (_jobQueueLock)
          {
            _jobs.Clear();
          }
          _stopGraphThread = false;
          _graphThreadWaitEvent = new AutoResetEvent(false);
          _graphThread = new Thread(GraphThread);
          _graphThread.Name = "RTL283x FM graph";
          _graphThread.SetApartmentState(ApartmentState.STA);
          _graphThread.IsBackground = false;
          _graphThread.Start();
        }

        GraphJob job = new GraphJob();
        job.JobType = jobType;
        job.Parameters = parameters;
        job.WaitEvent = new AutoResetEvent(false);

        lock (_jobQueueLock)
        {
          _jobs.Enqueue(job);
        }
        _graphThreadWaitEvent.Set();

        job.WaitEvent.WaitOne();
        job.WaitEvent.Close();
        if (job.ThrownException != null)
        {
          throw job.ThrownException;
        }
        return job.ReturnValue;
      }
    }

    /// <summary>
    /// Stop the thread that is used to interact with the graph.
    /// </summary>
    private void StopGraphThread()
    {
      lock (_graphThreadLock)
      {
        if (_graphThread != null)
        {
          if (!_graphThread.IsAlive)
          {
            this.LogWarn("RTL283x FM: aborting old graph thread");
            _graphThread.Abort();
          }
          else
          {
            _stopGraphThread = true;
            _graphThreadWaitEvent.Set();
            if (!_graphThread.Join(100))
            {
              this.LogWarn("RTL283x FM: failed to join graph thread, aborting thread");
              _graphThread.Abort();
            }
          }
          _graphThread = null;
          if (_graphThreadWaitEvent != null)
          {
            _graphThreadWaitEvent.Close();
            _graphThreadWaitEvent = null;
          }
        }
      }
    }

    private void GraphThread()
    {
      try
      {
        while (_graphThreadWaitEvent.WaitOne())
        {
          if (_stopGraphThread)
          {
            return;
          }

          lock (_jobQueueLock)
          {
            while (_jobs.Count > 0)
            {
              GraphJob job = _jobs.Dequeue();
              try
              {
                switch (job.JobType)
                {
                  case GraphJobType.Load:
                    InternalPerformLoading();
                    break;
                  case GraphJobType.Tune:
                    InternalPerformTuning(job.Parameters[0] as IChannel);
                    break;
                  case GraphJobType.UpdateSignalStatus:
                    InternalPerformSignalStatusUpdate((bool)job.Parameters[0]);
                    break;
                  case GraphJobType.SetGraphState:
                    InternalSetTunerState((TunerState)job.Parameters[0]);
                    break;
                  case GraphJobType.Unload:
                    InternalPerformUnloading();
                    break;
                  case GraphJobType.TsWriterSubChannelMethod:
                    job.ReturnValue = InternalInvokeTsWriterMethod(typeof(ITsFilter), ref job.Parameters);
                    break;
                  case GraphJobType.TsWriterScanMethod:
                    job.ReturnValue = InternalInvokeTsWriterMethod(typeof(ITsChannelScan), ref job.Parameters);
                    break;
                }
                job.WaitEvent.Set();
              }
              catch (ThreadAbortException)
              {
                throw;
              }
              catch (Exception ex)
              {
                job.ThrownException = ex;
                job.WaitEvent.Set();
              }
            }
          }
        }
      }
      catch (ThreadAbortException)
      {
      }
      catch (Exception ex)
      {
        this.LogError(ex, "RTL283x FM: graph thread exception");
      }
      finally
      {
        this.LogDebug("RTL283x FM: graph thread stopped");
      }
    }

    #endregion

    #region job wrappers

    /// <summary>
    /// Actually load the tuner.
    /// </summary>
    public override void PerformLoading()
    {
      object[] p = null;
      InvokeGraphJob(GraphJobType.Load, ref p);
    }

    /// <summary>
    /// Actually tune to a channel.
    /// </summary>
    /// <param name="channel">The channel to tune to.</param>
    public override void PerformTuning(IChannel channel)
    {
      object[] p = new object[1] { channel };
      InvokeGraphJob(GraphJobType.Tune, ref p);
    }

    /// <summary>
    /// Actually update tuner signal status statistics.
    /// </summary>
    /// <param name="onlyUpdateLock"><c>True</c> to only update lock status.</param>
    public override void PerformSignalStatusUpdate(bool onlyUpdateLock)
    {
      object[] p = new object[1] { onlyUpdateLock };
      InvokeGraphJob(GraphJobType.UpdateSignalStatus, ref p);
    }

    /// <summary>
    /// Set the state of the tuner.
    /// </summary>
    /// <param name="state">The state to apply to the tuner.</param>
    public override void SetTunerState(TunerState state)
    {
      object[] p = new object[1] { state };
      InvokeGraphJob(GraphJobType.SetGraphState, ref p);
    }

    /// <summary>
    /// Actually unload the tuner.
    /// </summary>
    public override void PerformUnloading()
    {
      object[] p = null;
      InvokeGraphJob(GraphJobType.Unload, ref p);
    }

    private int InvokeTsWriterSubChannelJob(ref object[] parameters)
    {
      return (int)InvokeGraphJob(GraphJobType.TsWriterSubChannelMethod, ref parameters);
    }

    private int InvokeTsWriterScanJob(ref object[] parameters)
    {
      return (int)InvokeGraphJob(GraphJobType.TsWriterScanMethod, ref parameters);
    }

    #endregion

    #region graph building

    /// <summary>
    /// Actually load the tuner.
    /// </summary>
    private void InternalPerformLoading()
    {
      this.LogDebug("RTL283x FM: perform loading");

      if (!DevicesInUse.Instance.Add(_mainTunerDevice))
      {
        throw new TvException("Tuner is in use.");
      }
      _mainTunerDeviceInUse = true;

      InitialiseGraph();

      // Normally the RTL283x driver only supports operation of one tuner in a
      // special mode. The driver selects this tuner by first match on a list
      // of friendly names located in the registry. We manipulate the registry
      // list and tuner name to ensure the driver matches this tuner. In theory
      // this should allow multiple tuners to operate in special modes.
      string originalListTunerName = null;
      string originalTunerName = _mainTunerDevice.Name;
      string fakeUniqueTunerName = "MediaPortal FM Tuner " + _tunerId;
      List<RegistryView> views = new List<RegistryView>() { RegistryView.Default };
      if (OSInfo.OSInfo.Is64BitOs() && IntPtr.Size != 8)
      {
        views.Add(RegistryView.Registry64);
      }
      foreach (RegistryView view in views)
      {
        RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).CreateSubKey(@"SYSTEM\CurrentControlSet\Services\RTL2832UBDA");
        try
        {
          if (string.IsNullOrEmpty(originalListTunerName))
          {
            originalListTunerName = (string)key.GetValue("FilterName1");
          }
          key.SetValue("FilterName1", fakeUniqueTunerName);
        }
        finally
        {
          key.Close();
        }
      }
      try
      {
        _mainTunerDevice.SetPropBagValue("FriendlyName", fakeUniqueTunerName);

        // After loading the source filter we have to put the registry back to
        // how it was before.
        try
        {
          _filterSource = FilterGraphTools.AddFilterFromRegisteredClsid(_graph, SOURCE_FILTER_CLSID, "RTL283x FM Source");
        }
        finally
        {
          _mainTunerDevice.SetPropBagValue("FriendlyName", originalTunerName);
        }
      }
      finally
      {
        foreach (RegistryView view in views)
        {
          RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).CreateSubKey(@"SYSTEM\CurrentControlSet\Services\RTL2832UBDA");
          try
          {
            key.SetValue("FilterName1", originalListTunerName);
          }
          finally
          {
            key.Close();
          }
        }
      }

      Capture capture = new Capture();
      capture.SetAudioCapture(_filterSource, null);
      _encoder = new EncoderRtl283xFm();
      _encoder.PerformLoading(_graph, null, capture);

      // Check for and load extensions, adding any additional filters to the graph.
      IBaseFilter lastFilter = _encoder.TsMultiplexerFilter;
      LoadExtensions(_filterSource, ref lastFilter);
      AddAndConnectTsWriterIntoGraph(lastFilter);
      CompleteGraph();

      _fmSource = _filterSource as IRtl283xFmSource;
      _staTsWriter = new TsWriterStaWrapper(InvokeTsWriterSubChannelJob, InvokeTsWriterScanJob);

      // RDS grabbing currently not supported.
      _epgGrabber = null;

      _channelScanner = new ChannelScannerDirectShowAnalog(this, _staTsWriter);
    }

    /// <summary>
    /// Actually unload the tuner.
    /// </summary>
    private void InternalPerformUnloading()
    {
      this.LogDebug("RTL283x FM: perform unloading");

      // This function is called from inside the graph thread, which means we
      // can't force the thread to stop from here. Setting the stop variable
      // and event should be enough to cause the thread to stop immediately
      // after this function finishes executing.
      _stopGraphThread = true;
      _graphThreadWaitEvent.Set();

      if (_filterSource != null)
      {
        if (_graph != null)
        {
          _graph.RemoveFilter(_filterSource);
        }
        Release.ComObject("RTL283x FM source filter", ref _filterSource);
      }
      _fmSource = null;

      if (_encoder != null)
      {
        _encoder.PerformUnloading(_graph);
        _encoder = null;
      }

      // Only remove the main tuner device from use when we registered it.
      if (_mainTunerDeviceInUse)
      {
        DevicesInUse.Instance.Remove(_mainTunerDevice);
        _mainTunerDeviceInUse = false;
        // Do NOT Dispose() or set the main tuner device to NULL. We would be
        // unable to reload.
      }

      CleanUpGraph();
    }

    /// <summary>
    /// Set the state of the tuner.
    /// </summary>
    /// <param name="state">The state to apply to the tuner.</param>
    private void InternalSetTunerState(TunerState state)
    {
      base.SetTunerState(state);
    }

    private int InternalInvokeTsWriterMethod(Type type, ref object[] parameters)
    {
      object[] p = null;
      if (parameters.Length > 1)
      {
        p = new object[parameters.Length - 1];
        Array.Copy(parameters, 1, p, 0, p.Length);
      }
      int returnValue = (int)(type.GetMethod((string)parameters[0]).Invoke(_filterTsWriter, p));
      if (parameters.Length > 1)
      {
        Array.Copy(p, 0, parameters, 1, p.Length);
      }
      return returnValue;
    }

    #endregion

    #region tuning & scanning

    /// <summary>
    /// Check if the tuner can tune to a specific channel.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the tuner can tune to the channel, otherwise <c>false</c></returns>
    public override bool CanTune(IChannel channel)
    {
      return channel is AnalogChannel && channel.MediaType == MediaTypeEnum.Radio;
    }

    /// <summary>
    /// Actually tune to a channel.
    /// </summary>
    /// <param name="channel">The channel to tune to.</param>
    private void InternalPerformTuning(IChannel channel)
    {
      this.LogDebug("RTL283x FM: perform tuning");
      AnalogChannel analogChannel = channel as AnalogChannel;
      if (analogChannel == null || channel.MediaType != MediaTypeEnum.Radio)
      {
        throw new TvException("Received request to tune incompatible channel.");
      }
      if (_fmSource.SetFrequency((int)analogChannel.Frequency / 1000) == Rtl283xFmResult.Fail)
      {
        throw new TvException("Failed to set frequency.");
      }
      _encoder.PerformTuning(analogChannel);
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      base.Dispose();
      if (_mainTunerDevice != null)
      {
        _mainTunerDevice.Dispose();
        _mainTunerDevice = null;
      }
    }

    #endregion
  }
}