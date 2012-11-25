﻿using System.Linq;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.Entities.Enums;

namespace Mediaportal.TV.Server.TVDatabase.EntityModel.Interfaces
{
  public interface IScheduleRepository : IRepository<Model>
  {
    IQueryable<Schedule> IncludeAllRelations(IQueryable<Schedule> query);
    IQueryable<Schedule> IncludeAllRelations(IQueryable<Schedule> query, ScheduleIncludeRelationEnum includeRelations);
  }
}
