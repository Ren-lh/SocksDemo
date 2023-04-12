using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Remote;

/// <summary>
/// 配置项
/// </summary>
internal class RemoteOptions
{

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// 缓冲区大小
    /// </summary>
    public int BuffSize { get; set; }
}
