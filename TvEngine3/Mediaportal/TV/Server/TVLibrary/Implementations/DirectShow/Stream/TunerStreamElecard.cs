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

namespace Mediaportal.TV.Server.TVLibrary.Implementations.DirectShow.Stream
{
  /// <summary>
  /// Implementation of <see cref="T:TvLibrary.Interfaces.ITVCard"/> which supports the Elecard
  /// IPTV source filter.
  /// </summary>
  internal class TunerStreamElecard : TunerStream
  {
    /// <summary>
    /// Initialise a new instance of the <see cref="TunerStreamElecard"/> class.
    /// </summary>
    /// <param name="sequenceNumber">A sequence number or index for this instance.</param>
    public TunerStreamElecard(int sequenceNumber)
      : base("Elecard Stream Source", sequenceNumber)
    {
      _defaultUrl = "elecard://0.0.0.0:1234:t=m2t/udp";
      _sourceFilterClsid = new Guid(0x62341545, 0x9318, 0x4671, 0x9d, 0x62, 0x9c, 0xaa, 0xcd, 0xd5, 0xd2, 0x0a);
    }
  }
}