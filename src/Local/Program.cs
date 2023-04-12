using Core;
using Local;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;


var localOptions = new ConfigurationBuilder().AddJsonFile("config.json", optional: true, reloadOnChange: true).Build().Get<LocalOptions>();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)//.AddConfiguration(config)
        .AddConsole();  //.AddNLog(config);
});

var logger = loggerFactory.CreateLogger("Local");

Cipher.Key = Password.GetPassBytes(localOptions.Password);

var localServer = new LocalServer(localOptions, logger);
localServer.Start();

await Task.CompletedTask;

Console.WriteLine("按下任意键暂停程序");
Console.ReadKey();
localServer.Stop();

Console.WriteLine("再按一次退出程序");
Console.ReadKey();


