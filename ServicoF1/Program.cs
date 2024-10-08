using Serilog;
using ServicoF1;
using ServicoF1.servicos;

AppDomain currentDomain = AppDomain.CurrentDomain;
currentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .UseSerilog((hostingcontext, loggerConfiguration) =>
    {
        loggerConfiguration.MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.File($"{AppDomain.CurrentDomain.BaseDirectory}\\logs\\log.txt", rollingInterval: RollingInterval.Month);
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<Startup>();
    })
    .Build();

await host.RunAsync();


static void MyHandler(object sender, UnhandledExceptionEventArgs args)
{
    Exception e = (Exception)args.ExceptionObject;
    string crashReport = $"[{DateTime.Now.ToLongDateString()}] | message: {e.Message}\n {e.InnerException}\n\n";
    File.AppendAllText($"{AppDomain.CurrentDomain.BaseDirectory}\\CrashReport.txt", crashReport);
}