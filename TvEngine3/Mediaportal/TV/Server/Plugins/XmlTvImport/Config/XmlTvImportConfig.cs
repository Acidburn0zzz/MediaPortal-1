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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Mediaportal.TV.Server.Common.Types.Enum;
using Mediaportal.TV.Server.Plugins.XmlTvImport.Service;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.TVControl.Interfaces.Services;
using Mediaportal.TV.Server.TVControl.ServiceAgents;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Simmetrics;
using MediaPortal.Common.Utils.ExtensionMethods;

namespace Mediaportal.TV.Server.Plugins.XmlTvImport.Config
{
  public partial class XmlTvImportConfig : SectionSettings
  {
    #region enums

    private enum MatchType
    {
      [Description("already mapped")]
      Mapped = 0,
      [Description("new mapping, exact match")]
      Exact,
      [Description("new mapping, partial match")]
      Partial,
      [Description("new mapping, no match")]
      None,
      [Description("broken mapping, exact match")]
      BrokenExact,
      [Description("broken mapping, partial match")]
      BrokenPartial,
      [Description("broken mapping, no match")]
      Broken,
      [Description("non-XMLTV")]
      External
    }

    #endregion

    private class ComboBoxChannelGroup
    {
      public string Name;
      public int Id;

      public ComboBoxChannelGroup(string groupName, int idGroup)
      {
        Name = groupName;
        Id = idGroup;
      }

      public override string ToString()
      {
        return Name;
      }
    }

    private class ComboBoxGuideChannel
    {
      public string DisplayName { get; set; }
      public string Id { get; set; }

      public ComboBoxGuideChannel(string displayName, string id)
      {
        DisplayName = displayName;
        Id = id;
      }

      public ComboBoxGuideChannel ValueMember
      {
        get
        {
          return this;
        }
      }

      public override string ToString()
      {
        return DisplayName;
      }
    }

    #region variables

    private IList<ChannelGroup> _channelGroups = null;
    private string _loadedGroupName = null;
    private int _loadedGroupId = 0;
    private Timer _statusUiUpdateTimer = null;

    private DateTime _importStatusDateTime = DateTime.MinValue;
    private string _importStatus = string.Empty;
    private string _importStatusChannelCounts = string.Empty;
    private string _importStatusProgramCounts = string.Empty;

    private DateTime _scheduledActionsStatusDateTime = DateTime.MinValue;
    private string _scheduledActionsStatus = string.Empty;

    #endregion

    public XmlTvImportConfig()
      : base("XMLTV Import")
    {
      ServiceAgents.Instance.AddGenericService<IXmlTvImportService>();
      InitializeComponent();
    }

    public override void OnSectionActivated()
    {
      this.LogDebug("XMLTV import config: activating");
      ISettingService settingServiceAgent = ServiceAgents.Instance.SettingServiceAgent;
      textBoxDataOrListFile.Text = settingServiceAgent.GetValue(XmlTvImportSetting.File, string.Empty);
      openFileDialogDataOrListFile.FileName = textBoxDataOrListFile.Text;

      checkBoxTimeCorrectionEnable.Checked = settingServiceAgent.GetValue(XmlTvImportSetting.UseTimeCorrection, false);
      numericUpDownTimeCorrectionHours.Value = settingServiceAgent.GetValue(XmlTvImportSetting.TimeCorrectionHours, 0);
      numericUpDownTimeCorrectionMinutes.Value = settingServiceAgent.GetValue(XmlTvImportSetting.TimeCorrectionMinutes, 0);
      textBoxDataOrListFile_TextChanged(null, null);

      checkBoxMappingsPartialMatch.Checked = settingServiceAgent.GetValue(XmlTvImportSetting.UsePartialMatching, false);

      checkBoxScheduledActionsDownload.Checked = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsDownload, false);
      textBoxScheduledActionsDownloadUrl.Text = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsDownloadUrl, "http://www.mysite.com/tvguide.xml");
      checkBoxScheduledActionsDownload_CheckedChanged(null, null);

      checkBoxScheduledActionsProgram.Checked = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsProgram, false);
      textBoxScheduledActionsProgramLocation.Text = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsProgramLocation, @"c:\Program Files\My Program\MyProgram.exe");
      openFileDialogScheduledActionsProgram.FileName = textBoxScheduledActionsProgramLocation.Text;
      checkBoxScheduledActionsProgram_CheckedChanged(null, null);

      numericUpDownScheduledActionsTimeFrequency.Value = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsTimeFrequency, 24);
      dateTimePickerScheduledActionsTimeBetweenStart.Value = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsTimeBetweenStart, DateTime.Now);
      dateTimePickerScheduledActionsTimeBetweenEnd.Value = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsTimeBetweenEnd, DateTime.Now);
      radioButtonScheduledActionsTimeStartup.Checked = settingServiceAgent.GetValue(XmlTvImportSetting.ScheduledActionsTimeOnStartup, false);
      radioButtonScheduledActionsTimeBetween.Checked = !radioButtonScheduledActionsTimeStartup.Checked;
      radioScheduledActionsTimeBetween_CheckedChanged(null, null);

      DebugSettings();

      try
      {
        comboBoxMappingsChannelGroup.Items.Clear();

        _channelGroups = ServiceAgents.Instance.ChannelGroupServiceAgent.ListAllChannelGroups(ChannelGroupIncludeRelationEnum.None);
        foreach (ChannelGroup group in _channelGroups)
        {
          comboBoxMappingsChannelGroup.Items.Add(new ComboBoxChannelGroup(string.Format("{0} - {1}", ((MediaType)group.MediaType).GetDescription(), group.GroupName), group.IdGroup));
        }
        comboBoxMappingsChannelGroup.SelectedIndex = 0;
      }
      catch (Exception ex)
      {
        this.LogError(ex, "XMLTV import config: failed to load channel groups");
      }

      labelImportStatusDateTimeValue.Text = string.Empty;
      labelImportStatusValue.Text = string.Empty;
      labelImportStatusChannelCountsValue.Text = string.Empty;
      labelImportStatusProgramCountsValue.Text = string.Empty;
      labelScheduledActionsStatusDateTimeValue.Text = string.Empty;
      labelScheduledActionsStatusValue.Text = string.Empty;
      UpdateImportAndScheduleStatusUi();
      _statusUiUpdateTimer = new Timer();
      _statusUiUpdateTimer.Interval = 10000;
      _statusUiUpdateTimer.Tick += new EventHandler(OnStatusUiUpdateTimerTick);
      _statusUiUpdateTimer.Enabled = true;
      _statusUiUpdateTimer.Start();
      base.OnSectionActivated();
    }

    public override void OnSectionDeActivated()
    {
      this.LogDebug("XMLTV import config: deactivating");
      SaveSettings();
      DebugSettings();
      _statusUiUpdateTimer.Enabled = false;
      _statusUiUpdateTimer.Stop();
      _statusUiUpdateTimer.Dispose();
      _statusUiUpdateTimer = null;
      base.OnSectionDeActivated();
    }

    private new void SaveSettings()
    {
      this.LogDebug("XMLTV import config: saving settings");
      ISettingService settingServiceAgent = ServiceAgents.Instance.SettingServiceAgent;
      settingServiceAgent.SaveValue(XmlTvImportSetting.File, textBoxDataOrListFile.Text);

      settingServiceAgent.SaveValue(XmlTvImportSetting.UseTimeCorrection, checkBoxTimeCorrectionEnable.Checked);
      settingServiceAgent.SaveValue(XmlTvImportSetting.TimeCorrectionHours, (int)numericUpDownTimeCorrectionHours.Value);
      settingServiceAgent.SaveValue(XmlTvImportSetting.TimeCorrectionMinutes, (int)numericUpDownTimeCorrectionMinutes.Value);

      settingServiceAgent.SaveValue(XmlTvImportSetting.UsePartialMatching, checkBoxMappingsPartialMatch.Checked);

      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsDownload, checkBoxScheduledActionsDownload.Checked);
      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsDownloadUrl, textBoxScheduledActionsDownloadUrl.Text);
      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsProgram, checkBoxScheduledActionsProgram.Checked);
      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsProgramLocation, textBoxScheduledActionsProgramLocation.Text);

      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsTimeFrequency, (int)numericUpDownScheduledActionsTimeFrequency.Value);
      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsTimeBetweenStart, dateTimePickerScheduledActionsTimeBetweenStart.Value);
      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsTimeBetweenEnd, dateTimePickerScheduledActionsTimeBetweenEnd.Value);
      settingServiceAgent.SaveValue(XmlTvImportSetting.ScheduledActionsTimeOnStartup, radioButtonScheduledActionsTimeStartup.Checked);
    }

    private void DebugSettings()
    {
      this.LogDebug("XMLTV import config: settings");
      this.LogDebug("  file                 = {0}", textBoxDataOrListFile.Text);
      this.LogDebug("  time correction...");
      this.LogDebug("    enabled            = {0}", checkBoxTimeCorrectionEnable.Checked);
      this.LogDebug("    hours              = {0}", numericUpDownTimeCorrectionHours.Value);
      this.LogDebug("    minutes            = {0}", numericUpDownTimeCorrectionMinutes.Value);
      this.LogDebug("  partial matching?    = {0}", checkBoxMappingsPartialMatch.Checked);
      this.LogDebug("  scheduled actions...");
      this.LogDebug("    download?          = {0}", checkBoxScheduledActionsDownload.Checked);
      this.LogDebug("    download URL       = {0}", textBoxScheduledActionsDownloadUrl.Text);
      this.LogDebug("    run program?       = {0}", checkBoxScheduledActionsProgram.Checked);
      this.LogDebug("    program location   = {0}", textBoxScheduledActionsProgramLocation.Text);
      this.LogDebug("    frequency          = {0} hour(s)", numericUpDownScheduledActionsTimeFrequency.Value);
      this.LogDebug("    on startup/resume? = {0}", radioButtonScheduledActionsTimeStartup.Checked);
      this.LogDebug("    between time start = {0}", dateTimePickerScheduledActionsTimeBetweenStart.Value.TimeOfDay);
      this.LogDebug("    between time end   = {0}", dateTimePickerScheduledActionsTimeBetweenEnd.Value.TimeOfDay);
    }

    private void OnStatusUiUpdateTimerTick(object sender, EventArgs e)
    {
      // Update the status UI. Take care to do it on the UI thread.
      this.Invoke((MethodInvoker)delegate
      {
        UpdateImportAndScheduleStatusUi();
      });
    }

    private void UpdateImportAndScheduleStatusUi()
    {
      ServiceAgents.Instance.PluginService<IXmlTvImportService>().GetImportStatus(out _importStatusDateTime, out _importStatus, out _importStatusChannelCounts, out _importStatusProgramCounts);
      if (_importStatusDateTime > DateTime.MinValue && (
        !string.Equals(labelImportStatusDateTimeValue.Text, _importStatusDateTime.ToString()) ||
        !string.Equals(labelImportStatusValue.Text, _importStatus) ||
        !string.Equals(labelImportStatusChannelCountsValue.Text, _importStatusChannelCounts) ||
        !string.Equals(labelImportStatusProgramCountsValue.Text, _importStatusProgramCounts)
      ))
      {
        labelImportStatusDateTimeValue.Text = _importStatusDateTime.ToString();
        labelImportStatusValue.Text = _importStatus;
        labelImportStatusChannelCountsValue.Text = _importStatusChannelCounts;
        labelImportStatusProgramCountsValue.Text = _importStatusProgramCounts;
        this.LogDebug("XMLTV import config: import status update...");
        this.LogDebug("  date/time = {0}", _importStatusDateTime);
        this.LogDebug("  status    = {0}", _importStatus);
        this.LogDebug("  channels  = {0}", _importStatusChannelCounts);
        this.LogDebug("  programs  = {0}", _importStatusProgramCounts);
      }

      ServiceAgents.Instance.PluginService<IXmlTvImportService>().GetScheduledActionsStatus(out _scheduledActionsStatusDateTime, out _scheduledActionsStatus);
      if (_scheduledActionsStatusDateTime > DateTime.MinValue && (
        !string.Equals(labelScheduledActionsStatusDateTimeValue.Text, _scheduledActionsStatusDateTime.ToString()) ||
        !string.Equals(labelScheduledActionsStatusValue.Text, _scheduledActionsStatus.ToString())
      ))
      {
        labelScheduledActionsStatusDateTimeValue.Text = _scheduledActionsStatusDateTime.ToString();
        labelScheduledActionsStatusValue.Text = _scheduledActionsStatus.ToString();
        this.LogDebug("XMLTV import config: scheduled actions status update...");
        this.LogDebug("  date/time = {0}", _scheduledActionsStatusDateTime);
        this.LogDebug("  status    = {0}", _scheduledActionsStatus);
      }
    }

    #region general tab

    private void buttonDataOrListFileBrowse_Click(object sender, EventArgs e)
    {
      if (openFileDialogDataOrListFile.ShowDialog() == DialogResult.OK)
      {
        textBoxDataOrListFile.Text = openFileDialogDataOrListFile.FileName;
      }
    }

    private void textBoxDataOrListFile_TextChanged(object sender, EventArgs e)
    {
      checkBoxTimeCorrectionEnable.Enabled = !textBoxDataOrListFile.Text.EndsWith(".lst", StringComparison.InvariantCultureIgnoreCase);
      checkBoxTimeCorrectionEnable_CheckedChanged(sender, e);
    }

    private void buttonImport_Click(object sender, EventArgs e)
    {
      this.LogDebug("XMLTV import config: force-starting import");
      SaveSettings();
      DebugSettings();
      ServiceAgents.Instance.PluginService<IXmlTvImportService>().ImportNow();
    }

    private void checkBoxTimeCorrectionEnable_CheckedChanged(object sender, EventArgs e)
    {
      bool enabled = checkBoxTimeCorrectionEnable.Enabled && checkBoxTimeCorrectionEnable.Checked;
      numericUpDownTimeCorrectionHours.Enabled = enabled;
      numericUpDownTimeCorrectionMinutes.Enabled = enabled;
    }

    #endregion

    #region mappings tab

    private void buttonMappingsLoad_Click(object sender, EventArgs e)
    {
      ComboBoxChannelGroup channelGroup = comboBoxMappingsChannelGroup.SelectedItem as ComboBoxChannelGroup;
      if (channelGroup == null)
      {
        return;
      }

      try
      {
        this.LogDebug("XMLTV import config: loading mappings for channel group, ID = {0}, name = {1}, partial match = {2}", channelGroup.Id, channelGroup.Name, checkBoxMappingsPartialMatch.Checked);
        textBoxMappingsAction.Text = "loading";
        dataGridViewMappings.Rows.Clear();

        // Load the database channels.
        IList<Channel> databaseChannels = ServiceAgents.Instance.ChannelServiceAgent.ListAllVisibleChannelsByGroupId(channelGroup.Id, ChannelIncludeRelationEnum.None);
        if (databaseChannels.Count == 0)
        {
          MessageBox.Show("There are no channels available to map.", MESSAGE_CAPTION);
          return;
        }

        // Load the guide channels from the server. The structure is:
        //    file name -> ID -> [display names]
        // Arrange the channels into 3 collections used for partial matching,
        // fast ID lookups, and display.
        IDictionary<string, IDictionary<string, IList<string>>> guideChannels = ServiceAgents.Instance.PluginService<IXmlTvImportService>().GetGuideChannelDetails();
        HashSet<string> guideChannelIds = new HashSet<string>();
        IDictionary<string, string> matchingDictionary = new Dictionary<string, string>(200);
        IList<ComboBoxGuideChannel> guideChannelsForComboBox = new List<ComboBoxGuideChannel>(200);
        IDictionary<string, ComboBoxGuideChannel> comboBoxValueLookup = new Dictionary<string, ComboBoxGuideChannel>(200);
        ComboBoxGuideChannel gc = new ComboBoxGuideChannel(string.Empty, string.Empty);
        guideChannelsForComboBox.Add(gc);
        comboBoxValueLookup.Add(gc.Id, gc);
        foreach (KeyValuePair<string, IDictionary<string, IList<string>>> fileChannels in guideChannels)
        {
          foreach (KeyValuePair<string, IList<string>> channel in fileChannels.Value)
          {
            string id = XmlTvImportId.GetQualifiedIdForChannel(fileChannels.Key, channel.Key);
            this.LogDebug("XMLTV import config: guide channel, ID = {0}, name(s) = [{1}]", id, string.Join(", ", channel.Value));
            if (!guideChannelIds.Add(id))
            {
              this.LogWarn("XMLTV import config: found multiple channels with ID {0}, won't be able to distinguish which data to use", id);
            }
            foreach (string displayName in channel.Value)
            {
              if (!matchingDictionary.ContainsKey(displayName))
              {
                matchingDictionary.Add(displayName, id);
              }
              else
              {
                this.LogWarn("XMLTV import config: found multiple channels named {0}, might match to wrong channel", displayName);
              }

              string itemName = displayName;
              if (!string.Equals(displayName, channel.Key))
              {
                itemName = string.Format("{0} ({1})", displayName, channel.Key);
              }
              gc = new ComboBoxGuideChannel(itemName, id);
              guideChannelsForComboBox.Add(gc);
              comboBoxValueLookup.Add(gc.Id, gc);
            }
          }
        }

        // Populate the grid.
        progressBarMappingsProgress.Minimum = 0;
        progressBarMappingsProgress.Maximum = databaseChannels.Count;
        progressBarMappingsProgress.Value = 0;
        dataGridViewColumnId.ValueType = typeof(int);
        dataGridViewMappings.Rows.Add(databaseChannels.Count);
        int row = 0;
        foreach (Channel channel in databaseChannels)
        {
          DataGridViewRow gridRow = dataGridViewMappings.Rows[row++];

          gridRow.Cells["dataGridViewColumnId"].Value = channel.IdChannel;
          gridRow.Cells["dataGridViewColumnTuningChannel"].Value = channel.Name;
          gridRow.Tag = channel;

          // Trust me - you don't want to mess with this! Data grid view combo
          // boxes are fragile.
          DataGridViewComboBoxCell guideChannelComboBox = (DataGridViewComboBoxCell)gridRow.Cells["dataGridViewColumnGuideChannel"];
          guideChannelComboBox.DataSource = guideChannelsForComboBox;
          guideChannelComboBox.ValueType = typeof(ComboBoxGuideChannel);
          guideChannelComboBox.DisplayMember = "DisplayName";
          guideChannelComboBox.ValueMember = "ValueMember";
          guideChannelComboBox.Tag = channel.ExternalId ?? string.Empty;

          // Find the best match guide channel for this channel.
          MatchType matchType = MatchType.None;
          string bestMatchId = string.Empty;
          if (!string.IsNullOrEmpty(channel.ExternalId))
          {
            if (!XmlTvImportId.HasXmlTvMapping(channel.ExternalId))
            {
              matchType = MatchType.External;
            }
            else
            {
              if (guideChannelIds.Contains(channel.ExternalId))
              {
                bestMatchId = channel.ExternalId;
                matchType = MatchType.Mapped;
              }
              else
              {
                // Check for mappings that have been broken by file renaming.
                string fileName;
                string xmlTvChannelId;
                XmlTvImportId.GetQualifiedIdComponents(channel.ExternalId, out fileName, out xmlTvChannelId);
                foreach (KeyValuePair<string, IDictionary<string, IList<string>>> fileChannels in guideChannels)
                {
                  if (fileChannels.Value.ContainsKey(xmlTvChannelId))
                  {
                    matchType = MatchType.Exact;
                    bestMatchId = XmlTvImportId.GetQualifiedIdForChannel(fileChannels.Key, xmlTvChannelId);
                    break;
                  }
                }
                if (matchType == MatchType.None)
                {
                  matchType = MatchType.Broken;
                }
              }
            }
          }

          if (matchType == MatchType.None || matchType == MatchType.Broken)
          {
            // Exact matching...
            if (matchingDictionary.TryGetValue(channel.Name, out bestMatchId))
            {
              if (matchType == MatchType.Broken)
              {
                matchType = MatchType.BrokenExact;
              }
              else
              {
                matchType = MatchType.Exact;
              }
            }

            // Partial matching...
            if (checkBoxMappingsPartialMatch.Checked && (matchType == MatchType.None || matchType == MatchType.Broken))
            {
              // Find the best match...
              float bestSimilarity = 0.5f;
              foreach (KeyValuePair<string, string> match in matchingDictionary)
              {
                float similarity = Levenshtein.GetSimilarity(match.Key, channel.Name);
                if (similarity > bestSimilarity)
                {
                  bestMatchId = match.Value;
                  bestSimilarity = similarity;
                  if (similarity == 1f)
                  {
                    break;
                  }
                }
              }

              if (!string.IsNullOrEmpty(bestMatchId))
              {
                if (matchType == MatchType.Broken)
                {
                  matchType = MatchType.BrokenPartial;
                }
                else
                {
                  matchType = MatchType.Partial;
                }
              }
            }
          }

          this.LogDebug("XMLTV import config: DB channel, ID = {0}, name = {1}, external ID = {2}, match type = {3}, best match = {4}", channel.IdChannel, channel.Name, channel.ExternalId ?? string.Empty, matchType, bestMatchId);
          if (!string.IsNullOrEmpty(bestMatchId))
          {
            guideChannelComboBox.Value = comboBoxValueLookup[bestMatchId];
          }

          // Note the mapping cell values are set so that the grid can be
          // sorted by mapping state without actually showing text in the
          // cells.
          DataGridViewCell cell = gridRow.Cells["dataGridViewColumnMatchType"];
          cell.ToolTipText = matchType.GetDescription();
          cell.Value = string.Empty.PadRight((int)matchType, ' ');
          if (matchType == MatchType.Mapped)
          {
            cell.Style.BackColor = Color.White;
          }
          else if (matchType == MatchType.Exact)
          {
            cell.Style.BackColor = Color.MediumSeaGreen;
          }
          else if (matchType == MatchType.Partial)
          {
            cell.Style.BackColor = Color.Orange;
          }
          else if (matchType == MatchType.None)
          {
            cell.Style.BackColor = Color.Red;
          }
          else if (matchType == MatchType.BrokenExact)
          {
            cell.Style.BackColor = Color.LightGreen;
          }
          else if (matchType == MatchType.BrokenPartial)
          {
            cell.Style.BackColor = Color.NavajoWhite;
          }
          else if (matchType == MatchType.Broken)
          {
            cell.Style.BackColor = Color.LightPink;
          }
          else if (matchType == MatchType.External)
          {
            cell.Style.BackColor = Color.LightGray;
          }

          progressBarMappingsProgress.Value++;
        }

        _loadedGroupId = channelGroup.Id;
        _loadedGroupName = channelGroup.Name;
        textBoxMappingsAction.Text = "loaded";
      }
      catch (Exception ex)
      {
        textBoxMappingsAction.Text = "load failed";
        this.LogError(ex, "XMLTV import config: failed to load channel group mappings, ID = {0}, name = {1}", channelGroup.Id, channelGroup.Name);
      }
    }

    private void buttonMappingsSave_Click(object sender, EventArgs e)
    {
      if (_loadedGroupName == null)
      {
        return;
      }
      this.LogDebug("XMLTV import config: saving mappings for channel group, ID = {0}, name = {1}", _loadedGroupId, _loadedGroupName);
      try
      {
        IList<Channel> channels = new List<Channel>(dataGridViewMappings.Rows.Count);

        progressBarMappingsProgress.Value = 0;
        progressBarMappingsProgress.Minimum = 0;
        progressBarMappingsProgress.Maximum = dataGridViewMappings.Rows.Count;
        textBoxMappingsAction.Text = "saving mappings";

        foreach (DataGridViewRow row in dataGridViewMappings.Rows)
        {
          DataGridViewComboBoxCell guideChannelCell = (DataGridViewComboBoxCell)row.Cells["dataGridViewColumnGuideChannel"];
          ComboBoxGuideChannel guideChannel = guideChannelCell.Value as ComboBoxGuideChannel;
          if (guideChannel == null)   // It seems the combobox value is null for the blank item.
          {
            guideChannel = new ComboBoxGuideChannel(string.Empty, string.Empty);
          }
          string previousExternalId = guideChannelCell.Tag as string;
          if (
            !string.Equals(guideChannel.Id, previousExternalId) &&
            // Don't touch non-XMLTV mappings unless the user has selected a guide channel.
            (
              string.IsNullOrEmpty(previousExternalId) ||
              XmlTvImportId.HasXmlTvMapping(previousExternalId) ||
              !string.IsNullOrEmpty(guideChannel.Id)
            )
          )
          {
            Channel channel = row.Tag as Channel;
            channel.ExternalId = guideChannel.Id;
            channels.Add(channel);
            this.LogDebug("XMLTV import config: mapped channel change, ID = {0}, name = {1}, old external ID = {2}, new external ID = {3}, guide name = {4}", channel.IdChannel, channel.Name, previousExternalId ?? "[null]", guideChannel.Id ?? "[null]", guideChannel.DisplayName);
          }
          progressBarMappingsProgress.Value++;
        }
        if (channels.Count > 0)
        {
          textBoxMappingsAction.Text = "mappings saved";
          ServiceAgents.Instance.ChannelServiceAgent.SaveChannels(channels);
        }
        else
        {
          textBoxMappingsAction.Text = "no changes";
        }
      }
      catch (Exception ex)
      {
        textBoxMappingsAction.Text = "save failed";
        this.LogError(ex, "XMLTV import config: failed to save channel group mappings, ID = {0}, name = {1}", _loadedGroupId, _loadedGroupName);
      }
    }

    #endregion

    #region schedule tab

    private void checkBoxScheduledActionsDownload_CheckedChanged(object sender, EventArgs e)
    {
      textBoxScheduledActionsDownloadUrl.Enabled = checkBoxScheduledActionsDownload.Checked;
      UpdateScheduledActionsTimeFields();
    }

    private void checkBoxScheduledActionsProgram_CheckedChanged(object sender, EventArgs e)
    {
      textBoxScheduledActionsProgramLocation.Enabled = checkBoxScheduledActionsProgram.Checked;
      buttonScheduledActionsProgramBrowse.Enabled = checkBoxScheduledActionsProgram.Checked;
      UpdateScheduledActionsTimeFields();
    }

    private void buttonScheduledActionsProgramBrowse_Click(object sender, EventArgs e)
    {
      if (openFileDialogScheduledActionsProgram.ShowDialog() == DialogResult.OK)
      {
        textBoxScheduledActionsProgramLocation.Text = openFileDialogScheduledActionsProgram.FileName;
      }
    }

    private void UpdateScheduledActionsTimeFields()
    {
      bool enabled = checkBoxScheduledActionsDownload.Checked || checkBoxScheduledActionsProgram.Checked;
      radioButtonScheduledActionsTimeBetween.Enabled = enabled;
      groupBoxScheduledActionsTime.Enabled = enabled;
    }

    private void radioScheduledActionsTimeBetween_CheckedChanged(object sender, EventArgs e)
    {
      dateTimePickerScheduledActionsTimeBetweenStart.Enabled = radioButtonScheduledActionsTimeBetween.Checked;
      dateTimePickerScheduledActionsTimeBetweenEnd.Enabled = radioButtonScheduledActionsTimeBetween.Checked;
    }

    private void dateTimePickerScheduledActionsTimeBetweenStart_ValueChanged(object sender, EventArgs e)
    {
      if (dateTimePickerScheduledActionsTimeBetweenStart.Value.TimeOfDay > dateTimePickerScheduledActionsTimeBetweenEnd.Value.TimeOfDay)
      {
        dateTimePickerScheduledActionsTimeBetweenStart.Value = dateTimePickerScheduledActionsTimeBetweenEnd.Value;
      }
    }

    private void dateTimePickerScheduledActionsTimeBetweenEnd_ValueChanged(object sender, EventArgs e)
    {
      if (dateTimePickerScheduledActionsTimeBetweenStart.Value.TimeOfDay > dateTimePickerScheduledActionsTimeBetweenEnd.Value.TimeOfDay)
      {
        dateTimePickerScheduledActionsTimeBetweenEnd.Value = dateTimePickerScheduledActionsTimeBetweenStart.Value;
      }
    }

    private void buttonScheduledActionsTimeNow_Click(object sender, EventArgs e)
    {
      this.LogDebug("XMLTV import config: force-starting scheduled actions");
      SaveSettings();
      DebugSettings();
      ServiceAgents.Instance.PluginService<IXmlTvImportService>().ExecuteScheduledActionsNow();
    }

    #endregion
  }
}