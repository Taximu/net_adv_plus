using System.Data;
using System.Globalization;
using System.Threading;
using Dapper;

namespace JobScheduler.DAL;

/// <summary>
/// Registers Dapper <see cref="SqlMapper.TypeHandler{T}"/>s so PostgreSQL <c>date</c>/<c>time</c> columns map to <see cref="DateOnly"/> / <see cref="TimeOnly"/> on materialize.
/// </summary>
public static class DapperNpgsqlConfiguration
{
    private static int _registered;

    public static void RegisterDateAndTimeHandlers()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0)
            return;

        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new NullableTimeOnlyTypeHandler());
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value) =>
            parameter.Value = value;

        public override DateOnly Parse(object value) =>
            value switch
            {
                DateOnly d => d,
                DateTime dt => DateOnly.FromDateTime(dt),
                _ => DateOnly.FromDateTime(Convert.ToDateTime(value, CultureInfo.InvariantCulture))
            };
    }

    private sealed class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly? value) =>
            parameter.Value = value.HasValue ? value.Value : DBNull.Value;

        public override DateOnly? Parse(object value) =>
            value is null or DBNull ? null : new DateOnlyTypeHandler().Parse(value);
    }

    private sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
    {
        public override void SetValue(IDbDataParameter parameter, TimeOnly value) =>
            parameter.Value = value.ToTimeSpan();

        public override TimeOnly Parse(object value) =>
            value switch
            {
                TimeOnly t => t,
                TimeSpan ts => TimeOnly.FromTimeSpan(ts),
                DateTime dt => TimeOnly.FromDateTime(dt),
                _ => TimeOnly.FromTimeSpan((TimeSpan)Convert.ChangeType(value, typeof(TimeSpan), CultureInfo.InvariantCulture)!)
            };
    }

    private sealed class NullableTimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, TimeOnly? value) =>
            parameter.Value = value.HasValue ? value.Value.ToTimeSpan() : DBNull.Value;

        public override TimeOnly? Parse(object value) =>
            value is null or DBNull ? null : new TimeOnlyTypeHandler().Parse(value);
    }
}
