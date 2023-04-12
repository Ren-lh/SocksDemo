using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Local;

/// <summary>
/// 配置项
/// </summary>
internal class LocalOptions
{

    /// <summary>
    /// 服务端ip
    /// </summary>
    //[JsonPropertyName("serverIp")]
    public string ServerIp { get; set; }

    /// <summary>
    /// 服务端-端口
    /// </summary>
    public int ServerPort { get; set; }

    /// <summary>
    /// 本地端-端口
    /// </summary>
    public int LocalPort { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// 缓冲区大小
    /// </summary>
    public int BuffSize { get; set; }
}

