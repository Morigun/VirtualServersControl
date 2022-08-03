using Microsoft.EntityFrameworkCore;

using VirtualServersControl;

var builder = WebApplication.CreateBuilder(args);

string connection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationContext>(options => options.UseSqlServer(connection));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/servers", async (ApplicationContext db) => await db.VirtualServers!.ToListAsync());
app.MapPost("/api/servers", async (VirtualServer server, ApplicationContext db) =>
{
    await db.VirtualServers!.AddAsync(server);
    await db.SaveChangesAsync();
    return server;
});
app.MapGet("/api/selectForRemove{id:int}", async (int id, ApplicationContext db) =>
{
    var server = await db.VirtualServers!.FirstOrDefaultAsync(server => server.VirtualServerID == id);
    if (server != null)
    {
        server.SelectedForRemove = !(server.SelectedForRemove ?? false);
        db.Update(server);
        await db.SaveChangesAsync();
        return Results.Json(server);
    }
    return Results.NotFound(id);
});
app.MapGet("/api/removeSelected", async (ApplicationContext db) =>
{
    var serevers = await db.VirtualServers!.Where(server => server.SelectedForRemove == true).ToListAsync();

    if (serevers.Any())
    {
        serevers.ForEach(server =>
        {
            server.SelectedForRemove = false;
            server.RemoveDateTime = DateTime.Now;
        });
        db.UpdateRange(serevers);
        await db.SaveChangesAsync();
        return Results.Json(serevers);
    }
    return Results.NotFound();
});
app.MapGet("/api/getUsageTime", async (ApplicationContext db) =>
{
    var serversList = await db.VirtualServers!.ToListAsync();
    var sortedList = serversList.OrderBy(b => b.CreateDateTime).ToList();
    var serverWithMinCreateDate = sortedList.Skip(0).First();
    TimeSpan timeRange = CalculateServersTotalUsagePeriod(serverWithMinCreateDate, sortedList);
    return $"{(timeRange.TotalDays != 0 ? $"{timeRange.Days}:" : "")}" +
           $"{(timeRange.TotalHours != 0 ? $"{timeRange.Hours}:" : "")}" +
           $"{timeRange.Minutes}:" +
           $"{timeRange.Seconds}";
});

app.Run();

static TimeSpan CalculateServersTotalUsagePeriod(VirtualServer serverWithMinCreateDate, List<VirtualServer> sortedList)
{
    TimeSpan timeRange = new();
    //ѕопытаемс€ без сложного алгоритма найти период
    if (TryGetTimePeriod(ref timeRange, sortedList, serverWithMinCreateDate))
        return timeRange;
    //≈сли не получилось, пойдем другим путем
    //—оздадим структуру на которой будем отмечать периоды
    Period period = new(serverWithMinCreateDate.CreateDateTime);
    //ѕревратим данные о серверах в периоды
    var serverPeriods = sortedList.Select(a => new StartEnd
    {
        Start = a.CreateDateTime,
        End = a.RemoveDateTime
    }).ToList();
    //Ѕежим по серверным периодам
    serverPeriods.ForEach(serverPeriod =>
    {
        //ѕровер€ем завершен ли период
        if (period.IsComplite)
            return;
        //≈сли это перва€ итераци€, то обработаем ее попроще
        if (period.StartTime == period.EndTime)
        {
            //«ададим начальное врем€ - дате создани€ сервера
            period.StartTime = serverPeriod.Start;
            //ѕроверим наличие даты окончани€ и установим ее значение если есть
            if (serverPeriod.End != null)
                period.EndTime = serverPeriod.End.Value;
            //ѕо хорошему такой ситуации не должно быть,
            //потому что мы обрабатываем ее в методе TryGetTimePeriod, но на вс€кий случай оставлю
            else
            {
                // онец периода ставим текущую дату
                period.EndTime = DateTime.Now;
                //«авершаем построение периода
                period.IsComplite = true;
            }
            //–ассчитываем врем€ периода
            period.TotalUsageTime = period.EndTime - period.StartTime;
        }
        //ƒл€ прочих серверов обработка будет чуть сложнее
        else
        {
            //ѕроверим что дата окончани€ больше чем дата создани€, тогда можем просто обработать
            //эти данные, от крайней даты, до даты окончани€ сервера
            if (period.EndTime >= serverPeriod.Start)
            {
                CalculatePeriod(ref period, serverPeriod);
            }
            //≈сли вдруг дата окончани€ меньше даты создани€, то есть пробел и его нужно учитывать
            else
            {
                //–ассчитываем длину пробела
                var space = serverPeriod.Start - period.EndTime;
                //ƒальше по аналогии выше, только еще вычитаем пробел
                if (CalculatePeriod(ref period, serverPeriod))
                    period.TotalUsageTime -= space;
            }
        }
    });
    return period.TotalUsageTime;
}

static bool TryGetTimePeriod(ref TimeSpan timeRange, List<VirtualServer> sortedList, VirtualServer serverWithMinCreateDate)
{
    //100% нашли врем€, от самого раннего созданного сервера, который еще не удален
    if (serverWithMinCreateDate.RemoveDateTime == null)
    {
        timeRange = DateTime.Now - serverWithMinCreateDate.CreateDateTime;
        return true;
    }
    //≈сли вдруг есть хоть один сервер, которые не удален, но был создан до удалени€ самого раннего
    //то тоже нашли период
    else if (sortedList.Any(server => server.RemoveDateTime == null &&
                                      server.CreateDateTime <= serverWithMinCreateDate.RemoveDateTime))
    {
        timeRange = DateTime.Now - serverWithMinCreateDate.CreateDateTime;
        return true;
    }
    return false;
}

static bool CalculatePeriod(ref Period period, StartEnd serverPeriod)
{
    if (serverPeriod.End != null)
    {
        if (serverPeriod.End > period.EndTime)
        {
            CalculateForRemoveMoreEnd(ref period, serverPeriod);
            return true;
        }
    }
    else
    {
        CalculateForEndNull(ref period);
        return true;
    }
    return false;
}
static void CalculateForRemoveMoreEnd(ref Period period, StartEnd serverPeriod)
{
    period.TotalUsageTime += serverPeriod.End!.Value - period.EndTime;
    period.EndTime = serverPeriod.End.Value;
}
static void CalculateForEndNull(ref Period period)
{
    period.TotalUsageTime += DateTime.Now - period.EndTime;
    period.EndTime = DateTime.Now;
    period.IsComplite = true;
}


struct Period
{
    public Period(DateTime startTime)
    {
        StartTime = startTime;
        EndTime = startTime;
    }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalUsageTime { get; set; } = new TimeSpan();
    public bool IsComplite = false;
}
struct StartEnd
{
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
}