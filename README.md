# 用C#从零开始写一个代理工具

众所周知的原因，外面世界的内容不是你想看就能看，为了突破自己人的封锁（GFW），借梯子翻墙是必须的，本文将教你从零开始实现一个代理工具。





## 什么是GFW

GFW：Great Firewall (of the People’s Republic of China)，中国国家防火墙，由于谐音 the Great Wall，所以也称长城防火墙，是中国政府在其互联网边界的审查系统（包括相关行政审查系统），具体指监控和过滤互联网内容的软硬件系统，由服务器和路由器等设备、相关的应用程序构成。



形象的描述，在没有 GFW 的时代，访问互联网的方式如下

![image](https://s1.ax1x.com/2023/01/31/pS0FHaV.png)



GFW 出现之后，则变成

![image](https://s1.ax1x.com/2023/01/31/pS0FLPU.png)



其内部构造如下

![image](https://s1.ax1x.com/2023/01/31/pS0FxM9.png)



它变成了本地浏览器和网站服务器之间的一道墙，阻拦了访问特定网站的请求，实际上，GFW 的拦截方式是多样的。

<br/>

## 翻墙的几种方式

- #### 修改Hosts文件

  足够简单的方式，以解决 GFW 的 DNS 污染和劫持的问题。Hosts 文件是一个储存计算机网络中各节点信息的文件，负责将主机名映射到相应的IP地址，通常用于补充或取代网络中DNS的功能。和DNS不同的是，用户可以直接对 Hosts 文件进行控制。



- #### SSH Tunnel

  SSH（Secure Shell）是一个提供数据通信安全、远程登录、远程指令执行等功能的安全网络协议。搭建一个 SSH 隧道翻墙，只需要有一台支持 SSH 的墙外服务器，且该服务器能 SSH 连接即可。

  ![image](https://s1.ax1x.com/2023/01/31/pS0FzrR.png)



- #### VPN

  对于企业来说，为了共享资源、协同工作，需要连接各地的分支机构，传统的专线联网方式价格昂贵，一般中小企业难以负担。这时低成本的VPN技术孕育而生。VPN（Virtual Private Network）即虚拟专用网络，利用廉价接入的公共网络（主要使用Inter-net）来传输私有数据，具有成本优势，因此被很多企业和电信运营商采用。

  

- #### Shadowsocks

  VPN之类的技术，有一个致命的缺陷：它们虽然通过了无法破解的RSA加密算法来对数据进行加密，但是在交换密钥、建立起隧道前仍在进行明文传输，GFW正是利用这一点，在交换秘钥的过程中暴力破解，这意味着接下来的加密没有任何意义。Shadowsocks所做的一点，就是提前规定好加密方式，解决 GFW 通过分析流量特征进行干扰的问题。

  

  技术原理上，Shadowsocks 是将原来 SSH 创建的 Socks5 协议拆开成 Server 端和 Client 端，其工作过程如下

  ![image](https://s1.ax1x.com/2023/01/31/pS0kCa6.png)

  相比传统的 VPN (IKE, IPSec, PPTP…)，Shadowsocks 协议具有更好的灵活性和隐蔽性，且搭建相对简单，可以拥有相对传统VPN更快的速度和更高的稳定性；而对比 V2Ray 这种科学上网的集合体，Shadowsocks在服务端更加轻量，单一协议完善程度更高；同时，Shadowsocks在移动端有更丰富的客户端选择，兼容性和灵活性更优。

  

- ##### V2Ray

  V2Ray 的实现原理跟Shadowsocks一脉相承，其特点在于引入新的通信协议，改正 Shadowsocks已有的缺点，使流量特征更难被 GFW 检测到；同时使用 UDP 作为下层协议，避免 TCP 带来的窗口和 RTO(Retry Timeout)等导致网速变慢的问题。

  

  <br/>

## 认识Shadowsocks

本文将实现一个简版的Shadowsocks，首先得了解一下Shadowsocks，Shadowsocks 是一个能骗过防火墙的代理工具，它把要传输的原数据经过加密后再传输，网络中的防火墙由于得不出要传输的原内容是什么而只好放行，于是就完成了防火墙穿透，也即是所谓的“翻墙”。



而Shadowsocks的原理就是把传输的数据加密，防火墙得到的数据是加密后的数据，不知道传输的原内容是什么，于是防火墙就放行本次请求，于是在本机就访问到了远程服务。



Shadowsocks 由两部分组成，运行在本地的 ss-local 和运行在防火墙之外服务器上的 ss-server，下面来分别详细介绍它们的职责



#### ss-local

ss-local 的职责是在本机启动和监听着一个服务，本地软件的网络请求都先发送到 ss-local，ss-local 收到来自本地软件的网络请求后，把要传输的原数据根据用户配置的加密方法和密码进行加密，再转发到墙外的服务器去。



#### ss-server

ss-server 的职责是在墙外服务器启动和监听一个服务，该服务监听来自本机的 ss-local 的请求。在收到来自 ss-local 转发过来的数据时，会先根据用户配置的加密方法和密码对数据进行对称解密，以获得加密后的数据的原内容。同时还会解 SOCKS5 协议，读出本次请求真正的目标服务地址(例如 Google 服务器地址)，再把解密后得到的原数据转发到真正的目标服务。

当真正的目标服务返回了数据时，ss-server 端会把返回的数据加密后转发给对应的 ss-local 端，ss-local 端收到数据再解密后，转发给本机的软件。这是一个对称相反的过程。

由于 ss-local 和 ss-server 端都需要用对称加密算法对数据进行加密和解密，因此这两端的加密方法和密码必须配置为一样。Shadowsocks 提供了一系列标准可靠的对称算法可供用户选择，例如 rc4、aes、des、chacha20 等等。Shadowsocks 对数据加密后再传输的目的是为了混淆原数据，让途中的防火墙无法得出传输的原数据。

<br/>

## SOCKS5 协议介绍

Shadowsocks 的数据传输是建立在 SOCKS5 协议之上的，SOCKS5 是 TCP/IP 层面的网络代理协议。
ss-server 端解密出来的数据就是采用 SOCKS5 协议封装的，通过 SOCKS5 协议 ss-server 端能读出本机软件想访问的服务的真正地址以及要传输的原数据，下面来详细介绍 SOCKS5 协议的通信细节。



#### 建立连接

客户端向服务端连接连接，客户端发送的数据包如下：

| VER  | NMETHODS | METHODS |
| ---- | -------- | ------- |
| 1    | 1        | 1       |

其中各个字段的含义如下：
-`VER`：代表 SOCKS 的版本，SOCKS5 默认为`0x05`，其固定长度为1个字节；
-`NMETHODS`：表示第三个字段METHODS的长度，它的长度也是1个字节；
-`METHODS`：表示客户端支持的验证方式，可以有多种，他的长度是1-255个字节。

目前支持的验证方式共有：

- `0x00`：NO AUTHENTICATION REQUIRED（不需要验证）

- `0x01`：GSSAPI

- `0x02`：USERNAME/PASSWORD（用户名密码）

- `0x03`: to X'7F' IANA ASSIGNED

- `0x80`: to X'FE' RESERVED FOR PRIVATE METHODS

- `0xFF`: NO ACCEPTABLE METHODS（都不支持，没法连接了）

  

#### 响应连接

服务端收到客户端的验证信息之后，就要回应客户端，服务端需要客户端提供哪种验证方式的信息。服务端回应的包格式如下：

| VER  | METHOD |
| ---- | ------ |
| 1    | 1      |

其中各个字段的含义如下：

- `VER`：代表 SOCKS 的版本，SOCKS5 默认为`0x05`，其固定长度为1个字节；
- `METHOD`：代表服务端需要客户端按此验证方式提供的验证信息，其值长度为1个字节，可为上面六种验证方式之一。

举例说明，比如服务端不需要验证的话，可以这么回应客户端：

| VER    | METHOD |
| ------ | ------ |
| `0x05` | `0x00` |



#### 和目标服务建立连接

客户端发起的连接由服务端验证通过后，客户端下一步应该告诉真正目标服务的地址给服务器，服务器得到地址后再去请求真正的目标服务。也就是说客户端需要把 Google 服务的地址`google.com:80`告诉服务端，服务端再去请求`google.com:80`。
目标服务地址的格式为 (IP或域名)+端口，客户端需要发送的包格式如下：

| VER  | CMD  | RSV    | ATYP | DST.ADDR | DST.PORT |
| ---- | ---- | ------ | ---- | -------- | -------- |
| 1    | 1    | `0x00` | 1    | Variable | 2        |

各个字段的含义如下：

- `VER`：代表 SOCKS 协议的版本，SOCKS 默认为0x05，其值长度为1个字节；

- ```
  CMD
  ```

  ：代表客户端请求的类型，值长度也是1个字节，有三种类型；

  - `CONNECT`： `0x01`；
  - `BIND`： `0x02`；
  - `UDP`： ASSOCIATE `0x03`；

- `RSV`：保留字，值长度为1个字节；

- ```
  ATYP
  ```

  ：代表请求的远程服务器地址类型，值长度1个字节，有三种类型；

  - `IPV4`： address: `0x01`；
  - `DOMAINNAME`: `0x03`；
  - `IPV6`： address: `0x04`；

- `DST.ADDR`：代表远程服务器的地址，根据 `ATYP` 进行解析，值长度不定；

- `DST.PORT`：代表远程服务器的端口，要访问哪个端口的意思，值长度2个字节。

服务端在得到来自客户端告诉的目标服务地址后，便和目标服务进行连接，不管连接成功与否，服务器都应该把连接的结果告诉客户端。在连接成功的情况下，服务端返回的包格式如下：

| VER  | REP  | RSV    | ATYP | BND.ADDR | BND.PORT |
| ---- | ---- | ------ | ---- | -------- | -------- |
| 1    | 1    | `0x00` | 1    | Variable | 2        |

各个字段的含义如下：

- `VER`：代表 SOCKS 协议的版本，SOCKS 默认为0x05，其值长度为1个字节；

- REP代表响应状态码，值长度也是1个字节，有以下几种类型

  - `0x00` succeeded
  - `0x01` general SOCKS server failure
  - `0x02` connection not allowed by ruleset
  - `0x03` Network unreachable
  - `0x04` Host unreachable
  - `0x05` Connection refused
  - `0x06` TTL expired
  - `0x07` Command not supported
  - `0x08` Address type not supported
  - `0x09` to `0xFF` unassigned

- `RSV`：保留字，值长度为1个字节

- ```
  ATYP
  ```

  ：代表请求的远程服务器地址类型，值长度1个字节，有三种类型

  - IP V4 address： `0x01`
  - DOMAINNAME： `0x03`
  - IP V6 address： `0x04`

- `BND.ADDR`：表示绑定地址，值长度不定。

- `BND.PORT`： 表示绑定端口，值长度2个字节



#### 数据转发

客户端在收到来自服务器成功的响应后，就会开始发送数据了，服务端在收到来自客户端的数据后，会转发到目标服务。



#### 总结

SOCKS5 协议的目的其实就是为了把来自原本应该在本机直接请求目标服务的流程，放到了服务端去代理客户端访问。
其运行流程总结如下：

1. 本机和代理服务端协商和建立连接；
2. 本机告诉代理服务端目标服务的地址；
3. 代理服务端去连接目标服务，成功后告诉本机；
4. 本机开始发送原本应发送到目标服务的数据给代理服务端，由代理服务端完成数据转发。

以上内容来自 [SOCKS5 协议规范 rfc1928](http://www.ietf.org/rfc/rfc1928.txt)。  





<br/>

## 用C#实现

要实现一个简版 Shadowsocks 需要实现两部分：运行在本地的 ss-local，和运行在墙外代理服务器上 ss-server。
下面分别教你如果使用 C# 来实现它们。

> 开发环境.Net6及以上。



#### 实现数据混淆

为了骗过防火墙，需要对数据进行混淆，在 Shadowsocks 中是采用的标准的对称加密算法去实现数据混淆的，对称算法在加密和解密过程中需要大量计算。
简单起见，我们采用以下方法实现数据混淆：



这个数据混淆算法和对称加密很相似，两端都需要有同样的密钥。
这个密钥有如下要求：

- 这个数组必须由 0～255 这256个数字组成，一个都不能差；

- 这个数组中第`I`个的值不能等于`I`；

  

例如以下为一个合法的密钥(上为索引，下为值)：

| 0    | 1    | 2    | 3    | 4    | 5    | 6    | 7    | 8    | 9    | 10   | 11   | 12   | 13   | 14   | 15   | 16   | 17   | 18   | 19   | 20   | 21   | 22   | 23   | 24   | 25   | 26   | 27   | 28   | 29   | 30   | 31   | 32   | 33   | 34   | 35   | 36   | 37   | 38   | 39   | 40   | 41   | 42   | 43   | 44   | 45   | 46   | 47   | 48   | 49   | 50   | 51   | 52   | 53   | 54   | 55   | 56   | 57   | 58   | 59   | 60   | 61   | 62   | 63   | 64   | 65   | 66   | 67   | 68   | 69   | 70   | 71   | 72   | 73   | 74   | 75   | 76   | 77   | 78   | 79   | 80   | 81   | 82   | 83   | 84   | 85   | 86   | 87   | 88   | 89   | 90   | 91   | 92   | 93   | 94   | 95   | 96   | 97   | 98   | 99   | 100  | 101  | 102  | 103  | 104  | 105  | 106  | 107  | 108  | 109  | 110  | 111  | 112  | 113  | 114  | 115  | 116  | 117  | 118  | 119  | 120  | 121  | 122  | 123  | 124  | 125  | 126  | 127  | 128  | 129  | 130  | 131  | 132  | 133  | 134  | 135  | 136  | 137  | 138  | 139  | 140  | 141  | 142  | 143  | 144  | 145  | 146  | 147  | 148  | 149  | 150  | 151  | 152  | 153  | 154  | 155  | 156  | 157  | 158  | 159  | 160  | 161  | 162  | 163  | 164  | 165  | 166  | 167  | 168  | 169  | 170  | 171  | 172  | 173  | 174  | 175  | 176  | 177  | 178  | 179  | 180  | 181  | 182  | 183  | 184  | 185  | 186  | 187  | 188  | 189  | 190  | 191  | 192  | 193  | 194  | 195  | 196  | 197  | 198  | 199  | 200  | 201  | 202  | 203  | 204  | 205  | 206  | 207  | 208  | 209  | 210  | 211  | 212  | 213  | 214  | 215  | 216  | 217  | 218  | 219  | 220  | 221  | 222  | 223  | 224  | 225  | 226  | 227  | 228  | 229  | 230  | 231  | 232  | 233  | 234  | 235  | 236  | 237  | 238  | 239  | 240  | 241  | 242  | 243  | 244  | 245  | 246  | 247  | 248  | 249  | 250  | 251  | 252  | 253  | 254  | 255  |
| ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- | ---- |
| 186  | 118  | 82   | 201  | 235  | 236  | 180  | 66   | 228  | 96   | 43   | 90   | 203  | 200  | 34   | 104  | 41   | 222  | 165  | 74   | 240  | 20   | 244  | 67   | 114  | 191  | 220  | 147  | 196  | 183  | 229  | 123  | 208  | 19   | 127  | 187  | 84   | 148  | 56   | 170  | 133  | 160  | 202  | 21   | 53   | 78   | 59   | 64   | 120  | 27   | 167  | 175  | 39   | 10   | 4    | 132  | 89   | 230  | 152  | 73   | 221  | 88   | 141  | 158  | 251  | 79   | 225  | 87   | 14   | 23   | 68   | 250  | 199  | 168  | 218  | 60   | 40   | 169  | 75   | 86   | 153  | 134  | 83   | 49   | 128  | 231  | 217  | 239  | 226  | 177  | 57   | 24   | 234  | 63   | 7    | 112  | 166  | 211  | 254  | 179  | 157  | 215  | 227  | 224  | 233  | 81   | 172  | 26   | 122  | 219  | 48   | 151  | 232  | 50   | 108  | 44   | 0    | 192  | 65   | 76   | 109  | 252  | 248  | 47   | 154  | 33   | 209  | 115  | 31   | 15   | 45   | 206  | 247  | 124  | 77   | 8    | 182  | 144  | 1    | 72   | 131  | 52   | 245  | 198  | 238  | 5    | 188  | 116  | 55   | 216  | 155  | 2    | 178  | 189  | 162  | 136  | 243  | 184  | 58   | 69   | 70   | 99   | 36   | 25   | 35   | 174  | 195  | 18   | 205  | 30   | 190  | 142  | 210  | 113  | 145  | 101  | 97   | 161  | 100  | 91   | 242  | 138  | 93   | 171  | 98   | 237  | 212  | 255  | 80   | 102  | 119  | 204  | 107  | 105  | 111  | 11   | 29   | 146  | 129  | 117  | 135  | 176  | 163  | 207  | 103  | 22   | 246  | 125  | 150  | 106  | 126  | 197  | 249  | 62   | 51   | 193  | 32   | 3    | 110  | 46   | 85   | 71   | 159  | 139  | 12   | 164  | 95   | 121  | 140  | 241  | 253  | 130  | 173  | 213  | 54   | 143  | 16   | 94   | 9    | 61   | 156  | 214  | 28   | 17   | 37   | 42   | 181  | 149  | 185  | 223  | 92   | 38   | 13   | 194  | 6    | 137  |

如果原数据为 `[5,0,1,2,3]`，则采用以上密钥加密后变成 `[236,186,118,82,201]`。
如果加密后的数据为 `[186,118,82,201,235]`，则采用以上密钥解密得到的原数据为 `[0,1,2,3,4]`

> 把1～255 这256个数字确定一种一对一的映射关系，加密是从一个数字得到对应的一个数字，而解密则是反向的过程，而这个密钥的作用正是描述这个映射关系。



为什么要这样设计数据混淆算法呢？在数据传输时，数据是以 byte 为最小单位流式传输的。一个 byte 的取值只可能是 0～255。该混淆算法可以直接对一个个 byte 进行加解密，而无需像标准的对称算法那样只能对一大块数据进行加密。
再加上本算法的加解密 N byte 数据的算法复杂度为 N（直接通过数组索引访问），非常适合流式加密。



以上加密算法虽然破绽很多，但足以实现高效的数据混淆，骗过防火墙。



产生一个以上密钥匙的代码如下：

```c#
namespace Core;

public class Password{
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
```



对数据进行加密解密的代码如下：

```c#
namespace Core;

public class Cipher{
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
```



<br/>

#### 实现 local 端

运行在本机的 local 端的职责是把本机程序发送给它的数据经过加密后转发给墙外的代理服务器，总体工作流程如下：

1. 监听来自本机浏览器的代理请求；
2. 转发前加密数据；
3. 转发socket数据到墙外代理服务端；
4. 把服务端返回的数据转发给用户的浏览器。

实现以上功能的 local 端代码如下：

```c#
namespace Local;

internal class TcpListen : IDisposable
{
    private int _port;
    private int _localPort;
    private readonly string _ip;

    private readonly TcpListener _tcpListener;
    
    public TcpListen(string ipAddr, int port, string pass, int localPort)
    {
        _ip = ipAddr;
        _port = port;
        _localPort = localPort;
        Cipher.Key = Password.GetPassBytes(pass);
        _tcpListener = new TcpListener(IPAddress.Any, _localPort);
    }

    public void Start()
    {
        _tcpListener.Start();
        _ = AcceptTcpAsync();
    }

    public void Stop()
    {
        _tcpListener.Stop();
    }

    private async Task AcceptTcpAsync()
    {
        try
        {
            while (true)
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                _ = new TcpLocal(tcpClient, new TcpClient(_ip, _port));
            }
        }
        catch (SocketException)
        {
        }
    }

    public void Dispose()
    {
        _tcpListener.Stop();
        GC.SuppressFinalize(this);
    }
}


internal class TcpLocal : IDisposable
{
    private readonly byte[] _proxyBuff = new byte[1024 * 50];
    private readonly byte[] _localBuff = new byte[1024 * 50];

    public static List<(TcpClient tcpClient, UdpLocal UdpLocal)> UdpList { get; private set; } = new();
    private readonly TcpClient _client;
    private readonly TcpClient _proxy;
    private readonly NetworkStream _clientStream;
    private readonly NetworkStream _proxyStream;
    public TcpLocal(TcpClient Client, TcpClient Proxy)
    {
        _client = Client;
        _proxy = Proxy;
        _clientStream = _client.GetStream();
        _proxyStream = _proxy.GetStream();
        _ = ClientReadAsync();
        _ = ProxyReadInitAsync(1);
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
                var data = Cipher.EnBytes(_localBuff[..recLen]);
                await TcpSendAsync(_proxyStream, data);
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException or TimeoutException)
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
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException or OperationCanceledException)
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _proxy.Dispose();
        GC.SuppressFinalize(this);
    }
}


```

调用：

```
var tcpListen = new TcpListen("Server端IP", 3080, "123456", 1080);
tcpListen.Start();
```





<br/>

#### 实现 server 端

运行在墙外代理服务器的 server 端职责如下：

1. 监听来自本地代理客户端的请求；
2. 解密本地代理客户端请求的数据，解析 SOCKS5 协议，连接用户浏览器真正想要连接的远程服务器；
3. 转发用户浏览器真正想要连接的远程服务器返回的数据的加密后的内容到本地代理客户端。

实现以上功能的代码如下：

```c#
namespace Server;

internal class TcpListen
{
    private const int buffSize = 1024 * 15;
    private const int timeout = 1000 * 5;
    private readonly byte[] _dataBuff = new byte[buffSize];
    private readonly TcpListener _tcpListener;

    public TcpListen(IPAddress ip, int port, string pass)
    {
        Cipher.Key = Password.GetPassBytes(pass);
        _tcpListener = new TcpListener(ip, port);
        Console.WriteLine($"Socks Service init,Listen on port {port}, udp support status : {udpSupport}");
    }

    public async Task StartAsync()
    {
        try
        {
            _tcpListener.Start();
            while (true)
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                tcpClient.ReceiveTimeout = timeout;
                _ = TcpConnectAsync(tcpClient);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    /// <summary>
    /// 发送数据
    /// </summary>
    /// <param name="tcpClient">TCPClient</param>
    /// <param name="data">数据</param>
    private async Task TcpSendAsync(TcpClient tcpClient, byte[] data)
    {
        await tcpClient.GetStream().WriteAsync(EnBytes(data));
    }

    /// <summary>
    /// 接受客户端连接
    /// </summary>
    /// <param name="tcpClient"></param>
    private async Task TcpConnectAsync(TcpClient tcpClient)
    {
        NetworkStream tcpStream = tcpClient.GetStream();
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var recLen = await tcpStream.ReadAsync(_dataBuff.AsMemory(0, buffSize), cts.Token);
            if (recLen == 0)
            {
                tcpClient.Dispose();
                return;
            }
            var data = Cipher.DeBytes(_dataBuff[..recLen]);
            var type = GetProxyType(data);
            //判断是否为无需账号密码模式
            bool isNoAuth = false;
            if (type is ProxyType.Connection)
            {
                var methodBytes = data.Skip(2).Take(data[1]);
                isNoAuth = methodBytes.Contains(byte.MinValue);
            }
            //首次请求建立连接
            if (isNoAuth)
            {
                Console.WriteLine($"receive connection request from {tcpClient.Client.RemoteEndPoint}");
                await TcpSendAsync(tcpClient, new byte[] { 5, 0 });
                _ = TcpConnectAsync(tcpClient);
            }
            //已建立连接,判断代理目标端信息
            else if (type is not ProxyType.Connection or ProxyType.Unknown)
            {
                var proxyInfo = GetProxyInfo(data);
                if (proxyInfo.Type is 1)
                {
                    //TCP
                    TcpClient tcpProxy = TcpConnecte(proxyInfo.IP, proxyInfo.Port);
                    if (tcpProxy.Connected)
                    {
                        _ = new TcpServer(tcpClient, tcpProxy);
                        await TcpSendAsync(tcpClient, new byte[] { 5, 0, 0, 1, 0, 0, 0, 0, 0, 0 });
                    }
                    else
                    {
                        await TcpSendAsync(tcpClient, new byte[] { 5, 255 });
                        throw new SocketException();
                    }
                }
                else if (proxyInfo.Type is 3)
                {
                    //UDP
                }
            }
            else
            {
                //不为连接且不为转发,有可能是密码错误
                throw new NotSupportedException("Unknown forwarding type or wrong password, this connection will be closed.");
            }
        }
        catch (Exception ex) when (ex is SocketException or NotSupportedException)
        {
            Close(tcpClient);
            Console.WriteLine(ex.Message);
        }
    }
    
    /// <summary>
    /// 建立TCP连接
    /// </summary>
    /// <param name="ip">IP</param>
    /// <param name="port">PORT</param>
    /// <returns></returns>
    private TcpClient TcpConnecte(IPAddress ip, int port)
    {
        TcpClient tcpClient = new();
        try
        {
            tcpClient.Connect(ip, port);
        }
        catch (SocketException)
        {
            tcpClient.Dispose();
        }
        return tcpClient;
    }
    
    /// <summary>
    /// 得到请求类型
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    private ProxyType GetProxyType(byte[] data)
    {
        if (data.Length > 2 && data.Length == data[1] + 2)
        {
            return ProxyType.Connection;
        }
        else if (data.Length > 8)
        {
            if (data[1] == 1)
            {
                ///TCP请求
                if (data[3] == 1 && data.Length == 10)
                {
                    return ProxyType.TcpProxyIPV4;
                }
                else if (data[3] == 3 && data.Length == (data.Skip(5).Take(data[4]).Count() + 7))
                {
                    return ProxyType.TcpProxyDomain;
                }
                else if (data[3] == 4 && data.Length == 22)
                {
                    return ProxyType.TcpProxyIPV6;
                }
            }
            else if (data[1] == 3)
            {
                //UDP请求或转发
                if (data[3] == 1 && data.Length == 10)
                {
                    return ProxyType.UdpProxyIPV4;
                }
                else if (data[3] == 3 && data.Length == (data.Skip(5).Take(data[4]).Count() + 7))
                {
                    return ProxyType.UdpProxyDomain;
                }
                else if (data[3] == 4 && data.Length == 22)
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
    /// <param name="data"></param>
    /// <returns>
    /// int type代理协议 -1 未知,1:TCP,3:UDP
    /// string IP
    /// int PORT
    /// </returns>
    public (int Type, IPAddress IP, int Port) GetProxyInfo(byte[] data)
    {
        IPAddress? hostIp = null;
        int port = 0;
        var type = GetProxyType(data);
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
                        Console.WriteLine($"Receive tcp Ipv4 proxy request to {hostIp}:{port}");
                        break;
                    case ProxyType.TcpProxyDomain or ProxyType.UdpProxyDomain:
                        //域名解析IP
                        string Realm_Name = Encoding.UTF8.GetString(data.Skip(5).Take(data[4]).ToArray());
                        hostIp = Dns.GetHostEntry(Realm_Name).AddressList[0];
                        portBytes = (data.Skip(5 + data[4]).Take(2).ToArray());
                        port = (portBytes[0] << 8) + portBytes[1];
                        Console.WriteLine($"Receive tcp proxy request to {Realm_Name}({hostIp}:{port})");
                        break;
                    case ProxyType.TcpProxyIPV6 or ProxyType.UdpProxyIPV6:
                        //IPV6
                        hostIp = new IPAddress(data.Skip(4).Take(16).ToArray());
                        portBytes = (data.Skip(8).Take(2).ToArray());
                        port = (portBytes[0] << 8) + portBytes[1];
                        Console.WriteLine($"Receive tcp Ipv6 proxy request to {hostIp}:{port}的代理请求");
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

    /// <summary>
    /// 关闭客户端连接
    /// </summary>
    /// <param name="tcpClient">客户端TCPClient</param>
    private static void Close(TcpClient tcpClient)
    {
        try
        {
            if (tcpClient.Connected)
            {
                Console.WriteLine($"Close the client connection to {tcpClient.Client.RemoteEndPoint}");
            }
        }
        catch (SocketException sex)
        {
            Console.WriteLine(sex.Message);
        }
        finally
        {
            tcpClient.Close();
        }
    }

}


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


internal class TcpServer
{
    private const int _buffSize = 1024 * 50;
    private readonly byte[] _clientBuff = new byte[_buffSize];
    private readonly byte[] _proxyBuff = new byte[_buffSize];
    private readonly TcpClient _client;
    private readonly TcpClient _proxy;
    private readonly NetworkStream _clientStream;
    private readonly NetworkStream _proxyStream;

    public TcpServer(TcpClient tcpClient, TcpClient tcpProxy)
    {
        _proxy = tcpProxy;
        _client = tcpClient;
        _clientStream = _client.GetStream();
        _proxyStream = _proxy.GetStream();
        _ = TcpClientReceive();
        _ = TcpProxyReceive();
        Console.WriteLine($"Open the tcp proxy tunnel to {_client.Client.RemoteEndPoint}");
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
    private async Task TcpClientReceive()
    {
        while (true)
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var recLen = await _clientStream.ReadAsync(_clientBuff.AsMemory(0, _buffSize), cts.Token);
                await TcpSendAsync(_proxyStream, DeBytes(_clientBuff[..recLen]));

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
    private async Task TcpProxyReceive()
    {
        try
        {
            while (true)
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
                var recLen = await _proxyStream.ReadAsync(_proxyBuff.AsMemory(0, _buffSize), cts.Token);
                await TcpSendAsync(_clientStream, EnBytes(_proxyBuff[..recLen]));
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
        if (_client.Connected)
        {

            Console.WriteLine($"Close the client connection to {_client.Client.RemoteEndPoint}");
            _client.Dispose();
        }
        if (_proxy.Connected)
        {
            Console.WriteLine($"Close the proxy connection to {_proxy.Client.RemoteEndPoint}");
            _proxy.Dispose();
        }
    }

    ~TcpServer()
    {
        _client?.Dispose();
        _proxy?.Dispose();
    }
}


```

调用：

```c#
var listener = new TcpListen(IPAddress.Any, 3080, "123456");
listener.StartAsync();
```



------



以上就是实现一个简版 Shadowsocks 的核心代码。

Local和Server都启动成功后，我们就来试试效果吧，由于我们常用 Chrome 浏览器，下面以 Chrome 为例讲解如何搭配我们写的程序进行科学上网。

## 安装 [SwitchyOmega](https://github.com/FelisCatus/SwitchyOmega)

第一步你需要安装 SwitchyOmega Chrome 插件，用于管理 Chrome 浏览器的网络代理。



## 配置 SwitchyOmega

#### 1. 先配置一个 SOCKS5 代理

![image](https://s1.ax1x.com/2023/01/31/pS0P8z9.png)



![image](https://s1.ax1x.com/2023/01/31/pS0Przd.png)

注意端口要写对，要和 Local 监听的 SOCKS5 端口一致。



#### 2. 再配置一个自动切换

![image 20230131115142678](https://s1.ax1x.com/2023/01/31/pS0P5WQ.png)

![image](https://s1.ax1x.com/2023/01/31/pS0PjFU.png)

`Rule List URL` 地址为 `https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt`。

![image](https://s1.ax1x.com/2023/01/31/pS0ipl9.png)

记得选中自动切换（ss-auto）

到此SwitchyOmega的设置就结束了。



现在我们访问google看看：

![image](https://s1.ax1x.com/2023/01/31/pS0Fhvj.png)

成功。