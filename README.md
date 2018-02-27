简易Http下载器(HttpDownloader.cs)
========================

使用方法
-------
```cs
HttpDownloader downloader = new HttpDownloader();
downloader.download_url = "http://download.somthing";
downloader.savefilepath = "c:/filename";
downloader.Start();
```

API
---

**void Start(ThreadPriority *priority* = ThreadPriority.BelowNormal)**

- priority 下载线程优先级，默认为ThreadPriority.BelowNormal

开始下载。

**void Abort()**

- 无参数

终止下载。

**void Close()**

- 无参数

关闭。

**void Dispose()**

- 无参数

关闭。同*Close()* 。

**string *download_url* { get; set; }**

属性。下载URL

**Status *status* { get; }**

属性。当前状态，定义如下

```cs
public enum Status
{
    /// <summary>
    /// 初始
    /// </summary>
    kNone  = 0,
    /// <summary>
    /// 开始
    /// </summary>
    kStarted = 1,
    /// <summary>
    /// 正在连接
    /// </summary>
    kConnecting = 2,
    /// <summary>
    /// 连接完成
    /// </summary>
    kConnected = 3,
    /// <summary>
    /// 正在下载
    /// </summary>
    kDownloading = 4,
    /// <summary>
    /// 完成
    /// </summary>
    kComplete = 5,
    
    /// <summary>
    /// 中断
    /// </summary>
    kAborted = 0x10,
    /// <summary>
    /// 超时
    /// </summary>
    kTimeout = 0x11,
    /// <summary>
    /// 错误
    /// </summary>
    kFailed = 0x12,
};
```

**OutputMode *output_mode* { get; }**

属性。输出模式。定义：

```cs
public enum OutputMode
{ 
    /// <summary>
    /// 未设置
    /// </summary>
    kNone,
    /// <summary>
    /// 文件输出
    /// </summary>
    kFile,
    /// <summary>
    /// 流输出
    /// </summary>
    kStream,
}
```

**long *currentsize* { get; }**

属性。下载大小，单位为Byte

**long *totalsize* { get; }**

属性。总大小，单位为Byte

**int *time_out* { get; set; }**

属性，默认为5000（5秒）。超时设置，单位毫秒。

**bool *md5_enable* { get; set; }**

属性，默认为true。MD5计算是否启用，请在开始下载前设置，开始下载后将无法设置！  

**string *md5_content* { get; }**

属性。下载完成后，下载内容的MD5值。未下载完成时为null。  
如果md5_enable=false，调用时将抛出 MD5NoEnabledException

**DateTime *start_time* { get; }**

属性。开始时间，未开始则返回null。

**DateTime *end_time* { get; }**

属性。开始时间，未开始则返回null。

**string *savefilepath* { get; set; }**

属性。下载保存文件  
注意与自定义输出流OutStream互斥

**Stream *outstream* { get; set;}**

属性。自定义输出流  
注意与下载文件LocalFilePath互斥

**event UpdateDelegate *update_event***

成员变量。更新事件。UpdateDelegate定义

```cs
/// <summary>
/// 下载更新回调
/// </summary>
/// <param name="message">消息</param>
/// <param name="status">状态</param>
/// <param name="currsize">下载完成大小</param>
/// <param name="totalsize">下载总大小</param>
/// <param name="remain">剩余时间</param>
public delegate void UpdateDelegate(string message, Status status,
                                    long currsize, long totalsize,
                                    TimeSpan remain);
```

