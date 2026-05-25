using Kanban.Api.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kanban.Api.Data.Configurations.Activities;

internal static class ActivityActionConverter
{
    public static readonly ValueConverter<ActivityAction, string> Instance =
        new(
            v => v.ToString().ToLowerInvariant(),
            v => Enum.Parse<ActivityAction>(v, true));
}
