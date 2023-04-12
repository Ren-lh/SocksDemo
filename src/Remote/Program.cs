using Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Remote;

var remoteOptions = new ConfigurationBuilder().AddJsonFile("config.json", optional: true, reloadOnChange: true).Build().Get<RemoteOptions>();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)//.AddConfiguration(config)
        .AddConsole();  //.AddNLog(config);
});

var logger = loggerFactory.CreateLogger("Remote");

Cipher.Key = Password.GetPassBytes(remoteOptions.Password);

var remoteServer = new RemoteServer(remoteOptions, logger);
remoteServer.Start();

await Task.CompletedTask;

Console.WriteLine("按下任意键暂停程序");
Console.ReadKey();
remoteServer.Stop();

Console.WriteLine("再按一次退出程序");
Console.ReadKey();

