using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using Core;
using System.Threading;

namespace Remote;

internal class RemoteServer
{
    private readonly RemoteOptions _options;
    private readonly ILogger _logger;

    private CancellationTokenSource _cancellationStop;
    private readonly TcpListener _tcpListener;

    private const int BUFF_SIZE = 8192;
    private readonly byte[] _dataBuff = new byte[BUFF_SIZE];

    public RemoteServer(RemoteOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        _tcpListener = new TcpListener(IPAddress.Any, options.Port);
    }
    ~RemoteServer()
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
        _logger?.LogInformation($"服务启动成功，端口: {_options.Port}");

        _ = Task.Run(async () =>
        {
            await ProcessTcp(_cancellationStop.Token);
        }, this._cancellationStop.Token);
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
            var tcpClient = await _tcpListener.AcceptTcpClientAsync();

            if (tcpClient is null)
            {
                _logger.LogInformation("ProcessTcp： client is null");
                break;
            }
            _ = TcpConnectAsync(tcpClient, cancellationToken);
        }
    }

    /// <summary>
    /// 接受客户端连接
    /// </summary>
    /// <param name="tcpClient"></param>
    private async Task TcpConnectAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        NetworkStream tcpStream = tcpClient.GetStream();
        tcpStream.ReadTimeout = 10000; // 设置读取超时时间为10秒
        tcpStream.WriteTimeout = 10000; // 设置写入超时时间为10秒
        try
        {
            //_ = _socks5Handler.HandleTcp(tcpClient, this._cancellationStop.Token);

            if (tcpClient is null)
                return;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var recLen = await tcpStream.ReadAsync(_dataBuff.AsMemory(0, BUFF_SIZE), cts.Token);
            if (recLen == 0)
            {
                tcpClient.Close();
                tcpClient.Dispose();
                return;
            }
            //解密
            var data = Cipher.DeBytes(_dataBuff[..recLen]);

            //获取请求类型
            var type = Utils.GetProxyType(data);

            //建立连接
            //判断 METHODS 是否不需要验证
            if (type is ProxyType.Connection && data[2] == 0x0)
            {
                _logger.LogInformation($"接收连接请求，来自: {tcpClient.Client.RemoteEndPoint}");
                //响应连接
                await tcpStream.WriteAsync(Cipher.EnBytes(new byte[] { 0x5, 0x0 }));
                _ = TcpConnectAsync(tcpClient, cancellationToken);
                return;
            }
            //判断是否已经连接
            if (type is (not ProxyType.Connection) or ProxyType.Unknown)
            {
                //获取请求转发信息
                var proxyInfo = Utils.GetProxyInfo(data, type, _logger);
                if (proxyInfo.Type is 1)
                {
                    //TCP
                    TcpClient tcpProxy = new();
                    tcpProxy.Connect(proxyInfo.IP, proxyInfo.Port);
                    if (tcpProxy.Connected)
                    {

                        _ = new RemoteHandler(tcpClient, tcpProxy, _options, cancellationToken);

                        await tcpClient.GetStream().WriteAsync(Cipher.EnBytes(new byte[] { 0x5, 0x0, 0x0, 0x1, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 }));
                    }
                    else
                    {
                        //await TcpSendAsync(tcpClient, Connect_Fail);
                        throw new SocketException();
                    }
                }
                else if (proxyInfo.Type is 3)
                {
                    //UDP 
                    _logger.LogInformation("UDP支持被禁用，UDP代理请求将被丢弃");
                }
            }
        }
        catch (Exception ex) when (ex is SocketException or NotSupportedException)
        {
            tcpClient.Close();
        }
    }


}

