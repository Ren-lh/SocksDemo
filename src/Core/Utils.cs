using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Core;


public enum ProxyType
{
    Connection = 1,
    TcpProxyIPV4,
    TcpProxyDomain,
    TcpProxyIPV6,
    UdpProxyIPV4,
    UdpProxyDomain,
    UdpProxyIPV6,
    Unknown = 500
}


public class Utils
{
    /// <summary>
    /// 得到请求类型
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static ProxyType GetProxyType(byte[] data)
    {
        if (data.Length > 2 && data.Length == data[1] + 2)
        {
            return ProxyType.Connection;
        }
        else if (data.Length > 8)
        {
            if (data[1] == 0x1)
            {
                ///TCP请求
                if (data[3] == 0x1 && data.Length == 10)
                {
                    return ProxyType.TcpProxyIPV4;
                }
                else if (data[3] == 0x3 && data.Length == (data.Skip(5).Take(data[4]).Count() + 7))
                {
                    return ProxyType.TcpProxyDomain;
                }
                else if (data[3] == 0x4 && data.Length == 22)
                {
                    return ProxyType.TcpProxyIPV6;
                }
            }
            else if (data[1] == 0x3)
            {
                //UDP请求或转发
                if (data[3] == 0x1 && data.Length == 10)
                {
                    return ProxyType.UdpProxyIPV4;
                }
                else if (data[3] == 0x3 && data.Length == (data.Skip(5).Take(data[4]).Count() + 7))
                {
                    return ProxyType.UdpProxyDomain;
                }
                else if (data[3] == 0x4 && data.Length == 22)
                {
                    return ProxyType.TcpProxyIPV6;
                }
            }
        }
        return ProxyType.Unknown;
    }


    /// <summary>
    /// 获取请求转发信息
    /// </summary>
    /// <param name="Data"></param>
    /// <returns>
    /// int type代理协议 -1 未知,1:TCP,3:UDP
    /// string IP
    /// int PORT
    /// </returns>
    public static (int Type, IPAddress IP, int Port) GetProxyInfo(byte[] data, ProxyType type, ILogger logger)
    {
        IPAddress hostIp = null;
        int port = 0;
        try
        {
            if (type is not ProxyType.Connection)
            {
                byte[] portBytes = new byte[2];
                switch (type)
                {
                    case ProxyType.TcpProxyIPV4 or ProxyType.UdpProxyIPV4:
                        //IPV4
                        hostIp = new IPAddress(data.Skip(4).Take(4).ToArray());
                        portBytes = (data.Skip(8).Take(2).ToArray());
                        port = (portBytes[0] << 8) + portBytes[1];
                        logger.LogInformation($"接收到tcp Ipv4代理请求: {hostIp}:{port}");
                        break;
                    case ProxyType.TcpProxyDomain or ProxyType.UdpProxyDomain:
                        //域名解析IP
                        string Realm_Name = Encoding.UTF8.GetString(data.Skip(5).Take(data[4]).ToArray());
                        hostIp = Dns.GetHostEntry(Realm_Name).AddressList[0];
                        portBytes = (data.Skip(5 + data[4]).Take(2).ToArray());
                        port = (portBytes[0] << 8) + portBytes[1];
                        logger.LogInformation($"接收到tcp代理请求: {Realm_Name}({hostIp}:{port})");
                        break;
                    case ProxyType.TcpProxyIPV6 or ProxyType.UdpProxyIPV6:
                        //IPV6
                        hostIp = new IPAddress(data.Skip(4).Take(16).ToArray());
                        portBytes = (data.Skip(8).Take(2).ToArray());
                        port = (portBytes[0] << 8) + portBytes[1];
                        logger.LogInformation($"接收到tcp Ipv6代理请求: {hostIp}:{port}的代理请求");
                        break;
                }

                if (hostIp != null)
                {
                    return (data[1], hostIp, port);
                }

            }
        }
        catch (IndexOutOfRangeException)
        {
        }
        return (0, IPAddress.Parse("127.0.0.1"), 0);
    }
}
