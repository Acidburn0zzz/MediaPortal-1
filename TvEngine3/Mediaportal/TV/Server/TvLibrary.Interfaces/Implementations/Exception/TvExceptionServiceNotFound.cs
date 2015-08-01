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
using Mediaportal.TV.Server.TVLibrary.Interfaces.Channel;

namespace Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Exception
{
  /// <summary>
  /// Exception thrown by the TV library when physical tuning is successful but
  /// the target service is not found in the broadcast stream.
  /// </summary>
  [Serializable]
  public class TvExceptionServiceNotFound : TvException
  {
    /// <summary>
    /// Initialise a new instance of the <see cref="TvExceptionServiceNotFound"/> class.
    /// </summary>
    /// <param name="channel">The tuning and details for the service that was not found.</param>
    public TvExceptionServiceNotFound(IChannel service)
      : base("Failed to find service after successful tuning.{0}{1}", Environment.NewLine, service)
    {
    }
  }
}