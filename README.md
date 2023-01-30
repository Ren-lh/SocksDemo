# 用C#从零开始写一个代理工具

众所周知的原因，外面世界的内容不是你想看就能看，为了突破自己人的封锁（GFW），借梯子翻墙是必须的，本文将教你从零开始实现一个代理工具。





## 什么是GFW

GFW：Great Firewall (of the People’s Republic of China)，中国国家防火墙，由于谐音 the Great Wall，所以也称长城防火墙，是中国政府在其互联网边界的审查系统（包括相关行政审查系统），具体指监控和过滤互联网内容的软硬件系统，由服务器和路由器等设备、相关的应用程序构成。



形象的描述，在没有 GFW 的时代，访问互联网的方式如下

![自由网络环境下的传输流程](https://user-images.githubusercontent.com/5773264/32371367-8c231024-c05e-11e7-87a8-f977577a6b89.png)



GFW 出现之后，则变成

![受限网络环境下的传输流程](https://user-images.githubusercontent.com/5773264/32371437-d8cb8852-c05e-11e7-9872-a6708bbe65ba.png)



其内部构造如下

![翻越GFW记/GFW内部结构](https://febers.github.io/%E7%BF%BB%E8%B6%8AGFW%E8%AE%B0/GFW%E5%86%85%E9%83%A8%E7%BB%93%E6%9E%84.jpg)



它变成了本地浏览器和网站服务器之间的一道墙，阻拦了访问特定网站的请求，实际上，GFW 的拦截方式是多样的。



## 翻墙的几种方式

- #### 修改Hosts文件

  足够简单的方式，以解决 GFW 的 DNS 污染和劫持的问题。Hosts 文件是一个储存计算机网络中各节点信息的文件，负责将主机名映射到相应的IP地址，通常用于补充或取代网络中DNS的功能。和DNS不同的是，用户可以直接对 Hosts 文件进行控制。



- #### SSH Tunnel

  SSH（Secure Shell）是一个提供数据通信安全、远程登录、远程指令执行等功能的安全网络协议。搭建一个 SSH 隧道翻墙，只需要有一台支持 SSH 的墙外服务器，且该服务器能 SSH 连接即可。

  ![SSH连接方式](https://febers.github.io/%E7%BF%BB%E8%B6%8AGFW%E8%AE%B0/SSH%E8%BF%9E%E6%8E%A5%E6%96%B9%E5%BC%8F.png)



- #### VPN

  对于企业来说，为了共享资源、协同工作，需要连接各地的分支机构，传统的专线联网方式价格昂贵，一般中小企业难以负担。这时低成本的VPN技术孕育而生。VPN（Virtual Private Network）即虚拟专用网络，利用廉价接入的公共网络（主要使用Inter-net）来传输私有数据，具有成本优势，因此被很多企业和电信运营商采用。

  

- #### Shadowsocks

  VPN之类的技术，有一个致命的缺陷：它们虽然通过了无法破解的RSA加密算法来对数据进行加密，但是在交换密钥、建立起隧道前仍在进行明文传输，GFW正是利用这一点，在交换秘钥的过程中暴力破解，这意味着接下来的加密没有任何意义。Shadowsocks所做的一点，就是提前规定好加密方式，解决 GFW 通过分析流量特征进行干扰的问题。

  

  技术原理上，Shadowsocks 是将原来 SSH 创建的 Socks5 协议拆开成 Server 端和 Client 端，其工作过程如下

  ![SS连接方式](https://febers.github.io/%E7%BF%BB%E8%B6%8AGFW%E8%AE%B0/SS%E8%BF%9E%E6%8E%A5%E6%96%B9%E5%BC%8F.png)

  相比传统的 VPN (IKE, IPSec, PPTP…)，Shadowsocks 协议具有更好的灵活性和隐蔽性，且搭建相对简单，可以拥有相对传统VPN更快的速度和更高的稳定性；而对比 V2Ray 这种科学上网的集合体，Shadowsocks在服务端更加轻量，单一协议完善程度更高；同时，Shadowsocks在移动端有更丰富的客户端选择，兼容性和灵活性更优。

  

- ##### V2Ray

  V2Ray 的实现原理跟Shadowsocks一脉相承，其特点在于引入新的通信协议，改正 Shadowsocks已有的缺点，使流量特征更难被 GFW 检测到；同时使用 UDP 作为下层协议，避免 TCP 带来的窗口和 RTO(Retry Timeout)等导致网速变慢的问题。

  



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







## 用C#实现

要实现一个简版 Shadowsocks 需要实现两部分：运行在本地的 ss-local，和运行在墙外代理服务器上 ss-server。
下面来分别教你如果使用 C# 来实现它们。



































