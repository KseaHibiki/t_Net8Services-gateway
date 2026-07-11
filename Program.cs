using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Gateway")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Service} | {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/gateway-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Service} | {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("redis")
    ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Redis 滑动窗口限流中间件
var permitLimit = builder.Configuration.GetValue<int>("RateLimit:PermitLimit", 100);
var windowSeconds = builder.Configuration.GetValue<int>("RateLimit:WindowSeconds", 10);

app.Use(async (context, next) =>
{
    var redis = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
    var db = redis.GetDatabase();

    // 使用客户端 IP 作为限流键
    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var key = $"ratelimit:{clientIp}";
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var windowStart = now - windowSeconds;

    // 使用有序集合实现滑动窗口
    var transaction = db.CreateTransaction();
    _ = transaction.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);
    _ = transaction.SortedSetAddAsync(key, now.ToString(), now);
    _ = transaction.KeyExpireAsync(key, TimeSpan.FromSeconds(windowSeconds + 1));

    var committed = await transaction.ExecuteAsync();
    if (!committed)
    {
        // 事务失败，放行请求
        await next();
        return;
    }

    var currentCount = await db.SortedSetLengthAsync(key, windowStart, now);
    if (currentCount > permitLimit)
    {
        Log.Warning("请求被限流: IP={IP}, Count={Count}, Limit={Limit}",
            clientIp, currentCount, permitLimit);
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.Response.WriteAsJsonAsync(new
        {
            Message = "请求过于频繁，请稍后重试",
            RetryAfter = windowSeconds
        });
        return;
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.MapControllers();
app.MapReverseProxy();

Log.Information("Gateway 启动完成，监听端口 5000，路由: /api/orders -> Shop, /api/inventory -> WMS");
app.Run();