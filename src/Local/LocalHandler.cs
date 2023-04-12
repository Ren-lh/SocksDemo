using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Core;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Local;

internal class LocalHandler : IDisposable
{
    private readonly byte[] _proxyBuff;
    private readonly byte[] _localBuff;

    private readonly ILogger _logger;
    private readonly LocalOptions _options;

    private readonly TcpClient _client;
    private readonly TcpClient _proxy;
    private readonly NetworkStream _clientStream;
    private readonly NetworkStream _proxyStream;

    public LocalHandler(TcpClient client, TcpClient proxy, LocalOptions options, ILogger logger) : this(options, logger)
    {
        _client = client;
        _proxy = proxy;
        _clientStream = _client.GetStream();
        _proxyStream = _proxy.GetStream();
        _ = ClientReadAsync();
        _ = ProxyReadInitAsync(1);
    }

    public LocalHandler(LocalOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        _proxyBuff = new byte[_options.BuffSize];
        _localBuff = new byte[_options.BuffSize];
    }

    public async Task TcpSendAsync(NetworkStream stream, byte[] data)
    {
        try
        {
            if (data.Length > 0)
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await stream.WriteAsync(data, cts.Token);
            }
            else
            {
                throw new SocketException();
            }
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException)
        {
            Dispose();
        }
    }

    /// <summary>
    /// 客户端加密后转发
    /// </summary>
    /// <returns></returns>
    private async Task ClientReadAsync()
    {
        try
        {
            while (true)
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var recLen = await _clientStream.ReadAsync(_localBuff, cts.Token);
                var data = _localBuff[..recLen];
                #region infos
                {
                    //获取请求类型
                    var type = Utils.GetProxyType(data);
                    //获取请求转发信息
                    var proxyInfo = Utils.GetProxyInfo(data, type, _logger);
                }
                #endregion
                var mData = Cipher.EnBytes(data);
                await TcpSendAsync(_proxyStream, mData);
            }
        }
        catch (Exception ex)// when (ex is SocketException or IOException or ObjectDisposedException or TimeoutException)
        {
            Dispose();
        }
    }


    /// <summary>
    /// 代理端接收初始化
    /// </summary>
    /// <param name="ar"></param>
    /// <returns></returns>
    private async Task ProxyReadInitAsync(int ar)
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var recLen = await _proxyStream.ReadAsync(_proxyBuff, cts.Token);
            var data = Cipher.DeBytes(_proxyBuff[..recLen]);
            if (ar == 2)
            {
                var remoteipEndpoint = _proxy.Client?.RemoteEndPoint as IPEndPoint;
                if (remoteipEndpoint is not null)
                {
                    await TcpSendAsync(_clientStream, data);
                    _ = ProxyReadAsync();
                }
            }
            else
            {
                await TcpSendAsync(_clientStream, data);
                _ = ProxyReadInitAsync(++ar);
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException or OperationCanceledException)
        {
            Dispose();
        }
    }

    /// <summary>
    /// 代理接收转发
    /// </summary>
    /// <returns></returns>
    private async Task ProxyReadAsync()
    {
        try
        {
            while (true)
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var recLen = await _proxyStream.ReadAsync(_proxyBuff, cts.Token);
                var data = Cipher.DeBytes(_proxyBuff[..recLen]);
                await TcpSendAsync(_clientStream, data);
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        //_client.Close();
        _client.Dispose();
        //_proxy.Close();
        _proxy.Dispose();
        GC.SuppressFinalize(this);
    }
}
