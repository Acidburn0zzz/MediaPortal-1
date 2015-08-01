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
using System.Collections.Generic;
using System.Windows.Forms;
using Mediaportal.TV.Server.Common.Types.Enum;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.SetupControls.UserInterfaceControls;
using Mediaportal.TV.Server.TVControl.ServiceAgents;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;

namespace Mediaportal.TV.Server.SetupTV.Sections
{
  public partial class ChannelMapping : SectionSettings
  {
    private class ChannelInfo
    {
      public int Id;
      public string Name;
      public int ImageIndex;

      public override string ToString()
      {
        return Name;
      }
    }

    private MediaType _mediaType = MediaType.Television;
    // broadcast standard -> [channel info]
    private IDictionary<BroadcastStandard, IList<ChannelInfo>> _channelInfoByBroadcastStandard = new Dictionary<BroadcastStandard, IList<ChannelInfo>>(10);
    // tuner ID -> channel ID -> mapping ID
    private IDictionary<int, IDictionary<int, int>> _tunerMappings = new Dictionary<int, IDictionary<int, int>>(50);

    private readonly MPListViewStringColumnSorter _listViewColumnSorterMapped;
    private readonly MPListViewStringColumnSorter _listViewColumnSorterNotMapped;

    public ChannelMapping(string name, MediaType mediaType)
      : base(name)
    {
      _mediaType = mediaType;
      InitializeComponent();
      _listViewColumnSorterMapped = new MPListViewStringColumnSorter();
      _listViewColumnSorterMapped.Order = SortOrder.Ascending;
      listViewNotMapped.ListViewItemSorter = _listViewColumnSorterMapped;
      _listViewColumnSorterNotMapped = new MPListViewStringColumnSorter();
      _listViewColumnSorterNotMapped.Order = SortOrder.Ascending;
      listViewMapped.ListViewItemSorter = _listViewColumnSorterNotMapped;
    }

    public override void OnSectionActivated()
    {
      this.LogDebug("mapping: activating, media type = {0}", _mediaType);
      _tunerMappings.Clear();

      // Build a dictionary of channel info keyed on broadcast standard. This
      // saves getting the channel list each time the tuner selection changes.
      _channelInfoByBroadcastStandard.Clear();
      IList<Channel> channels = ServiceAgents.Instance.ChannelServiceAgent.ListAllChannelsByMediaType(_mediaType, ChannelIncludeRelationEnum.TuningDetails);
      foreach (Channel c in channels)
      {
        ChannelInfo info = new ChannelInfo
        {
          Id = c.IdChannel,
          Name = c.Name,
          ImageIndex = GetImageIndex(c.TuningDetails)
        };
        BroadcastStandard seenTuningDetailBroadcastStandards = 0;
        foreach (TuningDetail td in c.TuningDetails)
        {
          BroadcastStandard tuningDetailBroadcastStandard = (BroadcastStandard)td.BroadcastStandard;
          if (!seenTuningDetailBroadcastStandards.HasFlag(tuningDetailBroadcastStandard))
          {
            IList<ChannelInfo> channelsByBroadcastStandard;
            if (!_channelInfoByBroadcastStandard.TryGetValue(tuningDetailBroadcastStandard, out channelsByBroadcastStandard))
            {
              channelsByBroadcastStandard = new List<ChannelInfo>(channels.Count);
              _channelInfoByBroadcastStandard.Add(tuningDetailBroadcastStandard, channelsByBroadcastStandard);
            }
            channelsByBroadcastStandard.Add(info);

            seenTuningDetailBroadcastStandards |= tuningDetailBroadcastStandard;
          }
        }
      }

      // Populate the tuner list.
      comboBoxTuner.Items.Clear();
      IList<Tuner> tuners = ServiceAgents.Instance.TunerServiceAgent.ListAllTuners(TunerIncludeRelationEnum.None);
      foreach (Tuner tuner in tuners)
      {
        if (tuner.IsEnabled && ServiceAgents.Instance.ControllerServiceAgent.IsCardPresent(tuner.IdTuner))
        {
          comboBoxTuner.Items.Add(tuner);
        }
      }
      if (comboBoxTuner.Items.Count > 0)
      {
        comboBoxTuner.SelectedIndex = 0;
      }
      this.LogDebug("mapping: channel count = {0}, tuner count = {1}", channels.Count, tuners.Count);

      base.OnSectionActivated();
    }

    private void comboBoxTuner_SelectedIndexChanged(object sender, EventArgs e)
    {
      listViewNotMapped.BeginUpdate();
      listViewNotMapped.ListViewItemSorter = null;
      listViewMapped.BeginUpdate();
      listViewMapped.ListViewItemSorter = null;
      try
      {
        listViewMapped.Items.Clear();
        listViewNotMapped.Items.Clear();

        Tuner tuner = comboBoxTuner.SelectedItem as Tuner;
        if (tuner == null)
        {
          return;
        }

        IDictionary<int, int> tunerMappings;
        if (!_tunerMappings.TryGetValue(tuner.IdTuner, out tunerMappings))
        {
          Tuner t = ServiceAgents.Instance.TunerServiceAgent.GetTuner(tuner.IdTuner, TunerIncludeRelationEnum.ChannelMaps);
          this.LogDebug("mapping: loading mappings, tuner ID = {0}, name = {1}, mapping count = {2}", tuner.IdTuner, tuner.Name, t.ChannelMaps.Count);
          tunerMappings = new Dictionary<int, int>(t.ChannelMaps.Count);
          foreach (ChannelMap map in t.ChannelMaps)
          {
            tunerMappings.Add(map.IdChannel, map.IdChannelMap);
          }
          _tunerMappings.Add(tuner.IdTuner, tunerMappings);
        }
        else
        {
          this.LogDebug("mapping: loading cached mappings, tuner ID = {0}, name = {1}, mapping count = {2}", tuner.IdTuner, tuner.Name, tunerMappings.Count);
        }

        BroadcastStandard tunerSupportedBroadcastStandards = (BroadcastStandard)tuner.SupportedBroadcastStandards;
        if (tunerSupportedBroadcastStandards == BroadcastStandard.Unknown)
        {
          this.LogError("mapping: tuner has no supported broadcast standards, ID = {0}, name = {1}", tuner.IdTuner, tuner.Name);
          return;
        }

        HashSet<int> seenChannels = new HashSet<int>();
        List<ListViewItem> itemsNotMapped = new List<ListViewItem>();
        List<ListViewItem> itemsMapped = new List<ListViewItem>();
        foreach (KeyValuePair<BroadcastStandard, IList<ChannelInfo>> broadcastStandardChannels in _channelInfoByBroadcastStandard)
        {
          if (tunerSupportedBroadcastStandards.HasFlag(broadcastStandardChannels.Key))
          {
            foreach (ChannelInfo channel in broadcastStandardChannels.Value)
            {
              if (!seenChannels.Contains(channel.Id))
              {
                seenChannels.Add(channel.Id);

                ListViewItem item = new ListViewItem(channel.Name, channel.ImageIndex);
                if (tunerMappings.ContainsKey(channel.Id))
                {
                  itemsNotMapped.Add(item);
                }
                else
                {
                  itemsMapped.Add(item);
                }
                item.Tag = channel;
              }
            }
          }
        }
        this.LogDebug("mapping: broadcast standard(s) = [{0}], mapped = {1}, not mapped = {2}", tunerSupportedBroadcastStandards, itemsMapped.Count, itemsNotMapped.Count);

        listViewNotMapped.Items.AddRange(itemsNotMapped.ToArray());
        listViewMapped.Items.AddRange(itemsMapped.ToArray());
      }
      finally
      {
        listViewNotMapped.ListViewItemSorter = _listViewColumnSorterNotMapped;
        listViewNotMapped.Sort();
        listViewNotMapped.EndUpdate();
        listViewMapped.ListViewItemSorter = _listViewColumnSorterMapped;
        listViewMapped.Sort();
        listViewMapped.EndUpdate();
      }
    }

    private int GetImageIndex(IList<TuningDetail> tuningDetails)
    {
      bool hasFta = false;
      bool hasScrambled = false;
      foreach (TuningDetail detail in tuningDetails)
      {
        if (detail.IsEncrypted)
        {
          hasScrambled = true;
        }
        else
        {
          hasFta = true;
        }
        if (hasFta && hasScrambled)
        {
          break;
        }
      }

      int imageIndex = 0;
      if (_mediaType == MediaType.Television)
      {
        if (hasFta && hasScrambled)
        {
          imageIndex = 5;
        }
        else if (hasScrambled)
        {
          imageIndex = 4;
        }
        else
        {
          imageIndex = 3;
        }
      }
      else if (_mediaType == MediaType.Radio)
      {
        if (hasFta && hasScrambled)
        {
          imageIndex = 2;
        }
        else if (hasScrambled)
        {
          imageIndex = 1;
        }
        else
        {
          imageIndex = 0;
        }
      }
      return imageIndex;
    }

    #region button and mouse event handlers

    private void buttonMap_Click(object sender, EventArgs e)
    {
      ListView.SelectedListViewItemCollection selectedItems = listViewNotMapped.SelectedItems;
      if (selectedItems == null || selectedItems.Count == 0)
      {
        return;
      }

      NotifyForm dlg = null;
      if (selectedItems.Count > 10)
      {
        dlg = new NotifyForm("Mapping selected channels to tuner...",
                                        "This can take some time." + Environment.NewLine + Environment.NewLine + "Please be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
      }

      try
      {
        Tuner tuner = comboBoxTuner.SelectedItem as Tuner;
        this.LogDebug("mapping: mapping {0} channel(s) to tuner {1}", selectedItems.Count, tuner.IdTuner);
        IDictionary<int, int> tunerMappings;
        if (!_tunerMappings.TryGetValue(tuner.IdTuner, out tunerMappings))
        {
          tunerMappings = new Dictionary<int, int>(selectedItems.Count);
          _tunerMappings.Add(tuner.IdTuner, tunerMappings);
        }

        listViewNotMapped.BeginUpdate();
        listViewNotMapped.ListViewItemSorter = null;
        listViewMapped.BeginUpdate();
        listViewMapped.ListViewItemSorter = null;
        try
        {
          ListViewItem[] items = new ListViewItem[selectedItems.Count];
          IList<int> oldMappingIds = new List<int>(selectedItems.Count);
          int i = 0;
          foreach (ListViewItem item in selectedItems)
          {
            ChannelInfo info = item.Tag as ChannelInfo;
            int mappingId;
            if (tunerMappings.TryGetValue(info.Id, out mappingId))
            {
              oldMappingIds.Add(mappingId);
              tunerMappings.Remove(info.Id);
            }
            listViewNotMapped.Items.Remove(item);
            items[i++] = item;
          }

          ServiceAgents.Instance.ChannelServiceAgent.DeleteChannelMaps(oldMappingIds);
          listViewMapped.Items.AddRange(items);
        }
        finally
        {
          listViewNotMapped.ListViewItemSorter = _listViewColumnSorterNotMapped;
          listViewNotMapped.EndUpdate();
          listViewMapped.ListViewItemSorter = _listViewColumnSorterMapped;
          listViewMapped.Sort();
          listViewMapped.EndUpdate();
        }
      }
      finally
      {
        if (dlg != null)
        {
          dlg.Close();
          dlg.Dispose();
        }
      }

      listViewMapped.Focus();
    }

    private void buttonUnmap_Click(object sender, EventArgs e)
    {
      ListView.SelectedListViewItemCollection selectedItems = listViewMapped.SelectedItems;
      if (selectedItems == null || selectedItems.Count == 0)
      {
        return;
      }

      NotifyForm dlg = null;
      if (selectedItems.Count > 10)
      {
        dlg = new NotifyForm("Unmapping selected channels from tuner...",
                                        "This can take some time." + Environment.NewLine + Environment.NewLine + "Please be patient...");
        dlg.Show(this);
        dlg.WaitForDisplay();
      }

      try
      {
        Tuner tuner = comboBoxTuner.SelectedItem as Tuner;
        this.LogDebug("mapping: unmapping {0} channel(s) from tuner {1}", selectedItems.Count, tuner.IdTuner);
        IDictionary<int, int> tunerMappings;
        if (!_tunerMappings.TryGetValue(tuner.IdTuner, out tunerMappings))
        {
          tunerMappings = new Dictionary<int, int>(selectedItems.Count);
          _tunerMappings.Add(tuner.IdTuner, tunerMappings);
        }

        listViewNotMapped.BeginUpdate();
        listViewNotMapped.ListViewItemSorter = null;
        listViewMapped.BeginUpdate();
        listViewMapped.ListViewItemSorter = null;
        try
        {
          ListViewItem[] items = new ListViewItem[selectedItems.Count];
          IList<ChannelMap> newMappings = new List<ChannelMap>(selectedItems.Count);
          int i = 0;
          foreach (ListViewItem item in selectedItems)
          {
            ChannelInfo info = item.Tag as ChannelInfo;
            newMappings.Add(new ChannelMap()
            {
              IdChannel = info.Id,
              IdTuner = tuner.IdTuner
            });
            listViewMapped.Items.Remove(item);
            items[i++] = item;
          }

          newMappings = ServiceAgents.Instance.ChannelServiceAgent.SaveChannelMaps(newMappings);
          foreach (ChannelMap mapping in newMappings)
          {
            tunerMappings.Add(mapping.IdChannel, mapping.IdChannelMap);
          }
          listViewNotMapped.Items.AddRange(items);
        }
        finally
        {
          listViewNotMapped.ListViewItemSorter = _listViewColumnSorterNotMapped;
          listViewNotMapped.Sort();
          listViewNotMapped.EndUpdate();
          listViewMapped.ListViewItemSorter = _listViewColumnSorterMapped;
          listViewMapped.EndUpdate();
        }
      }
      finally
      {
        if (dlg != null)
        {
          dlg.Close();
          dlg.Dispose();
        }
      }

      listViewNotMapped.Focus();
    }

    private void buttonMapAll_Click(object sender, EventArgs e)
    {
      foreach (ListViewItem item in listViewNotMapped.Items)
      {
        item.Selected = true;
      }
      buttonMap_Click(sender, e);
    }

    private void buttonUnmapAll_Click(object sender, EventArgs e)
    {
      foreach (ListViewItem item in listViewMapped.Items)
      {
        item.Selected = true;
      }
      buttonUnmap_Click(sender, e);
    }

    private void listViewMapped_MouseDoubleClick(object sender, MouseEventArgs e)
    {
      buttonUnmap_Click(null, null);
    }

    private void listViewNotMapped_MouseDoubleClick(object sender, MouseEventArgs e)
    {
      buttonMap_Click(null, null);
    }

    #endregion

    #region sorting

    private void listViewNotMapped_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _listViewColumnSorterNotMapped.SortColumn)
      {
        // Reverse the current sort direction for this column.
        _listViewColumnSorterNotMapped.Order = _listViewColumnSorterNotMapped.Order == SortOrder.Ascending
                                   ? SortOrder.Descending
                                   : SortOrder.Ascending;
      }
      else
      {
        // Set the column number that is to be sorted; default to ascending.
        _listViewColumnSorterNotMapped.SortColumn = e.Column;
        _listViewColumnSorterNotMapped.Order = SortOrder.Ascending;
      }

      // Perform the sort with these new sort options.
      listViewNotMapped.Sort();
    }

    private void listViewMapped_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      if (e.Column == _listViewColumnSorterMapped.SortColumn)
      {
        // Reverse the current sort direction for this column.
        _listViewColumnSorterMapped.Order = _listViewColumnSorterMapped.Order == SortOrder.Ascending
                                   ? SortOrder.Descending
                                   : SortOrder.Ascending;
      }
      else
      {
        // Set the column number that is to be sorted; default to ascending.
        _listViewColumnSorterMapped.SortColumn = e.Column;
        _listViewColumnSorterMapped.Order = SortOrder.Ascending;
      }

      // Perform the sort with these new sort options.
      listViewMapped.Sort();
    }

    #endregion

    #region drag and drop

    private bool IsValidDragDropData(DragEventArgs e, MPListView expectListView)
    {
      MPListView listView = e.Data.GetData(typeof(MPListView)) as MPListView;
      if (listView == null || listView != expectListView)
      {
        e.Effect = DragDropEffects.None;
        return false;
      }
      e.Effect = DragDropEffects.Move;
      return true;
    }

    private void listViewNotMapped_ItemDrag(object sender, ItemDragEventArgs e)
    {
      listViewNotMapped.DoDragDrop(listViewNotMapped, DragDropEffects.Move);
    }

    private void listViewNotMapped_DragOver(object sender, DragEventArgs e)
    {
      IsValidDragDropData(e, listViewMapped);
    }

    private void listViewNotMapped_DragEnter(object sender, DragEventArgs e)
    {
      IsValidDragDropData(e, listViewMapped);
    }

    private void listViewNotMapped_DragDrop(object sender, DragEventArgs e)
    {
      if (IsValidDragDropData(e, listViewMapped))
      {
        buttonUnmap_Click(null, null);
      }
    }

    private void listViewMapped_ItemDrag(object sender, ItemDragEventArgs e)
    {
      listViewMapped.DoDragDrop(listViewMapped, DragDropEffects.Move);
    }

    private void listViewMapped_DragOver(object sender, DragEventArgs e)
    {
      IsValidDragDropData(e, listViewNotMapped);
    }

    private void listViewMapped_DragEnter(object sender, DragEventArgs e)
    {
      IsValidDragDropData(e, listViewNotMapped);
    }

    private void listViewMapped_DragDrop(object sender, DragEventArgs e)
    {
      if (IsValidDragDropData(e, listViewNotMapped))
      {
        buttonMap_Click(null, null);
      }
    }

    #endregion
  }
}