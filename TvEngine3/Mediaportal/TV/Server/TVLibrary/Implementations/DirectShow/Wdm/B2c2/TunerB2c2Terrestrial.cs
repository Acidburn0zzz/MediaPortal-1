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

using Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow.Wdm.B2c2.Enum;
using Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow.Wdm.B2c2.Struct;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;

namespace Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow.Wdm.B2c2
{
  /// <summary>
  /// An implementation of <see cref="T:TvLibrary.Interfaces.ITVCard"/> for TechniSat terrestrial tuners with B2C2 chipsets and WDM drivers.
  /// </summary>
  internal class TunerB2c2Terrestrial : TunerB2c2Base
  {
    /// <summary>
    /// Initialise a new instance of the <see cref="TunerB2c2Terrestrial"/> class.
    /// </summary>
    /// <param name="info">The B2C2-specific information (<see cref="DeviceInfo"/>) about the tuner.</param>
    public TunerB2c2Terrestrial(DeviceInfo info)
      : base(info, CardType.DvbT)
    {
    }

    #region tuning

    /// <summary>
    /// Check if the tuner can tune to a specific channel.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the tuner can tune to the channel, otherwise <c>false</c></returns>
    public override bool CanTune(IChannel channel)
    {
      return channel is DVBTChannel;
    }

    /// <summary>
    /// Actually tune to a channel.
    /// </summary>
    /// <param name="channel">The channel to tune to.</param>
    public override void PerformTuning(IChannel channel)
    {
      this.LogDebug("B2C2 terrestrial: set tuning parameters");
      DVBTChannel dvbtChannel = channel as DVBTChannel;
      if (dvbtChannel == null)
      {
        throw new TvException("Received request to tune incompatible channel.");
      }

      lock (_tunerAccessLock)
      {
        HResult.ThrowException(_interfaceData.SelectDevice(_deviceInfo.DeviceId), "Failed to select device.");
        HResult.ThrowException(_interfaceTuner.SetFrequency((int)dvbtChannel.Frequency / 1000), "Failed to set frequency.");
        HResult.ThrowException(_interfaceTuner.SetBandwidth(dvbtChannel.Bandwidth / 1000), "Failed to set bandwidth.");

        // Note: it is not guaranteed that guard interval auto detection is supported, but if it isn't
        // then we can't tune - we have no idea what the actual value should be.
        HResult.ThrowException(_interfaceTuner.SetGuardInterval(GuardInterval.Auto), "Failed to use automatic guard interval detection.");
        base.PerformTuning(channel);
      }
    }

    #endregion
  }
}