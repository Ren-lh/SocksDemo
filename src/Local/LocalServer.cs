using Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Local;

internal class LocalServer
{
    private readonly LocalOptions _options;
    private readonly ILogger _logger;

    private CancellationTokenSource _cancellationStop;

    private readonly TcpListener _tcpListener;

    public LocalServer(LocalOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        _tcpListener = new TcpListener(IPAddress.Any, options.LocalPort);
    }
    ~LocalServer()
    {
        Stop();
    }


    [MethodImpl(MethodImplOptions.Synchronized)]
    public void Start()
    {
        Stop();

        _cancellationStop ??= new CancellationTokenSource();

        _logger?.LogInformation("服务启动中...");
        _tcpListener.Start();
        _logger?.LogInformation($"服务启动成功，本地端口: {_options.LocalPort}");

        _ = ProcessTcp(_cancellationStop.Token);
    }

    public void Stop()
    {
        _cancellationStop?.Cancel();
        _cancellationStop = null;

        _tcpListener?.Stop();
    }


    private async ValueTask ProcessTcp(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                if (tcpClient is null)
                {
                    _logger.LogInformation("ProcessTcp： client is null");
                    break;
                }
                //_socks5Handler.HandleTcp(tcpClient, this._cancellationStop.Token);

                _ = new LocalHandler(tcpClient, new TcpClient(_options.ServerIp, _options.ServerPort), _options, _logger);
            }
            catch (Exception ex) when (ex is SocketException)
            {
                _logger.LogError($"连接远程服务器 {_options.ServerIp} 失败: {ex.Message}");
                //throw;
            }
        }
    }

}
