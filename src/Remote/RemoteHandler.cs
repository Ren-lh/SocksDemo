using Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Remote;

internal class RemoteHandler
{
    private readonly byte[] _clientBuff;
    private readonly byte[] _proxyBuff;

    private readonly RemoteOptions _options;

    private readonly TcpClient _client;
    private readonly TcpClient _proxy;
    private readonly NetworkStream _clientStream;
    private readonly NetworkStream _proxyStream;

    private CancellationTokenSource _cancellation;

    public RemoteHandler(TcpClient tcpClient, TcpClient tcpProxy, RemoteOptions options, CancellationToken cancellationToken) : this(options)
    {
        _proxy = tcpProxy;
        _client = tcpClient;
        _clientStream = _client.GetStream();
        _proxyStream = _proxy.GetStream();

        _cancellation = new CancellationTokenSource();
        var lnkCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellation.Token);

        _ = TcpClientReceive(lnkCts.Token);
        _ = TcpProxyReceive(lnkCts.Token);
        Console.WriteLine($"Open the tcp proxy tunnel to {_client.Client.RemoteEndPoint}");
    }

    public RemoteHandler(RemoteOptions options)
    {
        _options = options;
        _clientBuff = new byte[_options.BuffSize];
        _proxyBuff = new byte[_options.BuffSize];
    }

    ~RemoteHandler()
    {
        _client.Close();
        _client.Dispose();
        _proxy.Close();
        _proxy.Dispose();
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="stream">TCP流</param>
    /// <param name="data">数据</param>
    private async Task TcpSendAsync(NetworkStream stream, byte[] data)
    {
        try
        {
            if (data.Length > 0)
            {
                await stream.WriteAsync(data);
            }
            else
            {
                throw new SocketException();
            }
        }
        catch (SocketException)
        {
            ProxyClose();
        }
    }

    /// <summary>
    /// 客户端接收数据回调
    /// </summary>
    /// <param name="ar"></param>
    private async Task TcpClientReceive(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var recLen = await _clientStream.ReadAsync(_clientBuff.AsMemory(0, _options.BuffSize), cts.Token);
                await TcpSendAsync(_proxyStream, Cipher.DeBytes(_clientBuff[..recLen]));

            }
            catch (SocketException)
            {
                ProxyClose();
            }
        }
    }

    /// <summary>
    /// 代理端接收数据回调
    /// </summary>
    /// <param name="ar"></param>
    private async Task TcpProxyReceive(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var recLen = await _proxyStream.ReadAsync(_proxyBuff.AsMemory(0, _options.BuffSize), cts.Token);
                await TcpSendAsync(_clientStream, Cipher.EnBytes(_proxyBuff[..recLen]));
            }
        }
        catch (SocketException)
        {
            ProxyClose();
        }
    }

    /// <summary>
    /// 关闭代理隧道
    /// </summary>
    private void ProxyClose()
    {
        _cancellation?.Cancel();
        //_cancellation?.Dispose();

        if (_client.Connected)
        {
            Console.WriteLine($"Close the client connection to {_client.Client.RemoteEndPoint}");
            _client.Close();
            _client.Dispose();
        }
        if (_proxy.Connected)
        {
            Console.WriteLine($"Close the proxy connection to {_proxy.Client.RemoteEndPoint}");
            _proxy.Close();
            _proxy.Dispose();
        }
    }
}
