#region Copyright (C) 2005-2011 Team MediaPortal

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

namespace Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces
{
  #region event delegates

  /// <summary>
  /// Delegate for the audio/video observer event.
  /// </summary>
  /// <param name="pidType">The type of stream that has been observed.</param>
  public delegate void AudioVideoObserverEvent(PidType pidType);

  #endregion

  /// <summary>
  /// Sub-Channel interface in TsWriter
  /// </summary>
  public interface ITvSubChannel
  {
    #region properties

    /// <summary>
    /// Gets the sub-channel id.
    /// </summary>
    /// <value>The sub-channel id.</value>
    int SubChannelId { get; }

    /// <summary>
    /// gets the current filename used for timeshifting
    /// </summary>
    string TimeShiftFileName { get; }

    /// <summary>
    /// returns the date/time when timeshifting has been started for the card specified
    /// </summary>
    /// <returns>DateTime containg the date/time when timeshifting was started</returns>
    DateTime StartOfTimeShift { get; }

    /// <summary>
    /// returns the date/time when recording has been started for the card specified
    /// </summary>
    /// <returns>DateTime containg the date/time when recording was started</returns>
    DateTime RecordingStarted { get; }

    /// <summary>
    /// Returns true when unscrambled audio/video is received otherwise false
    /// </summary>
    /// <returns>true of false</returns>
    bool IsReceivingAudioVideo { get; }

    /// <summary>
    /// gets the current filename used for recording
    /// </summary>
    string RecordingFileName { get; }

    /// <summary>
    /// returns true if card is currently recording
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// returns true if card is currently timeshifting
    /// </summary>
    bool IsTimeShifting { get; }

    /// <summary>
    /// returns the IChannel to which the card is currently tuned
    /// </summary>
    IChannel CurrentChannel { get; set; }

    #endregion

    /// <summary>
    /// Reload the sub-channel's configuration.
    /// </summary>
    void ReloadConfiguration();

    #region timeshifting and recording

    /// <summary>
    /// Starts timeshifting. Note card has to be tuned first
    /// </summary>
    /// <param name="fileName">filename used for the timeshiftbuffer</param>
    /// <returns>true if succeeded else false</returns>
    bool StartTimeShifting(string fileName);

    /// <summary>
    /// Stops timeshifting
    /// </summary>
    /// <returns>true if succeeded else false</returns>
    bool StopTimeShifting();

    /// <summary>
    /// Starts recording
    /// </summary>
    /// <param name="fileName">filename to which to recording should be saved</param>
    /// <returns>true if succeeded else false</returns>
    bool StartRecording(string fileName);

    /// <summary>
    /// Stop recording
    /// </summary>
    /// <returns>true if succeeded else false</returns>
    bool StopRecording();

    /// <summary>
    /// Returns the position in the current timeshift file and the id of the current timeshift file
    /// </summary>
    /// <param name="position">The position in the current timeshift buffer file</param>
    /// <param name="bufferId">The id of the current timeshift buffer file</param>
    void TimeShiftGetCurrentFilePosition(ref long position, ref long bufferId);

    /// <summary>
    /// Cancel the current tuning process.
    /// </summary>
    void CancelTune();

    #endregion

    event OnAfterTuneDelegate AfterTuneEvent;
    event AudioVideoObserverEvent AudioVideoEvent;
    void OnBeforeTune();
    void OnGraphRunning();
    void OnAfterTune();
    void Decompose();

    /// <summary>
    /// Fetch stream quality information from TsWriter.
    /// </summary>   
    /// <param name="totalBytes">The number of packets processed.</param>    
    /// <param name="discontinuityCounter">The number of stream discontinuities.</param>
    void GetStreamQualityCounters(out int totalBytes, out int discontinuityCounter);
  }
}