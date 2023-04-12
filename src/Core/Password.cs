using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core;

public class Password
{
    /// <summary>
    /// 得到密匙
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static List<byte> GetPassBytes(string key)
    {
        int passBytes = 0;
        foreach (var i in Encoding.UTF8.GetBytes(key))
            passBytes += i;
        var random = new Random(passBytes);
        var bytesPass = new List<byte>();
        for (int i = 0; i < 256; i++)
        {
            byte randomInt = (byte)random.Next(256);
            if (!bytesPass.Contains(randomInt))
                bytesPass.Add(randomInt);
            else
                i--;
        }
        return bytesPass;
    }
}
