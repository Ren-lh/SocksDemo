using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core;

public class Cipher
{
    public static List<byte> Key { set; private get; } = new();

    /// <summary>
    /// 加密
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static byte[] EnBytes(byte[] bytes)
    {
        var byteList = new List<byte>();
        foreach (var b in bytes)
        {
            byteList.Add((byte)Key.IndexOf(b));
        }
        return byteList.ToArray();
    }

    /// <summary>
    /// 解密
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static byte[] DeBytes(byte[] bytes)
    {
        var byteList = new List<byte>();
        foreach (var b in bytes)
        {
            byteList.Add(Key[b]);
        }
        return byteList.ToArray();
    }
}
