/* Copyright xerysherry 2018
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections;
using System.Net;
using System.IO;
using System.Threading;
using System.Security.Cryptography;

public class HttpDownloader : IDisposable
{
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
    /// <summary>
    /// 缓存大小
    /// </summary>
    public static int BuffSize = 10 * 1024;
    /// <summary>
    /// MD5计算未启用时，查询属性md5_content抛出的异常
    /// </summary>
    public class MD5NoEnabledException : Exception
    {
        public MD5NoEnabledException()
            : base("Please set md5_enable = true!")
        { }
    }

    /// <summary>
    /// 状态
    /// </summary>
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
    /// <summary>
    /// 输出方式
    /// </summary>
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

    public HttpDownloader()
    {
        stop_timer_ = new System.Timers.Timer(500);
        stop_timer_.Elapsed += new System.Timers.ElapsedEventHandler(StopTimer_Elapsed);
    }
    /// <summary>
    /// 关闭
    /// </summary>
    public void Close()
    {
        try
        {
            outstream_.Close();
        }
        catch { }

        try
        {
            if(stop_timer_ != null)
            {
                stop_timer_.Dispose();
                stop_timer_ = null;
            }
            http_response_.Close();
        }
        catch
        { }

        try
        {
            thread_stop_flag_ = true;
            Thread.Sleep(500);
            thread_.Abort();
            thread_ = null;
        }
        catch
        { }
    }
    /// <summary>
    /// 关闭
    /// </summary>
    public void Dispose()
    {
        Close();
    }

    /// <summary>
    /// 开始
    /// </summary>
    public void Start(ThreadPriority priority = ThreadPriority.BelowNormal)
    {
        thread_stop_flag_ = false;
        abort_flag_ = false;
        ThreadStart thr_start_func = new ThreadStart(Worker);
        thread_ = new Thread(thr_start_func);
        thread_.Priority = priority;
        thread_.IsBackground = true;
        thread_.Start();
        status_ = Status.kStarted;
    }
    /// <summary>
    /// 终止
    /// </summary>
    public void Abort()
    {
        if(abort_flag_ || thread_stop_flag_)
            return;
        abort_flag_ = true;
        thread_stop_flag_ = true;
        stop_timer_.Start();
    }

    private void Worker()
    {
        Downloading(download_url_);
    }
    private void Downloading(string url)
    {
        System.Timers.Timer timeout_timer = null;
        WebProxy wpxy = null;
        MD5 md5 = null;

        //设置开始时间
        start_time_ = DateTime.Now;
        try
        {
            //超时计时器
            timeout_timer = new System.Timers.Timer(time_out_);
            timeout_timer.Elapsed += new System.Timers.ElapsedEventHandler(TimeoutTimer_Elapsed);

            if(!string.IsNullOrEmpty(savefilepath_))
                outstream_ = new FileStream(savefilepath_, FileMode.OpenOrCreate, FileAccess.Write);
            else if(outstream_ == null)
            {
                //没有设定本地文件，或者自定义输出流
                SendUpdateEvent(Status.kNone, 0, 0);
                return;
            }

            http_request_ = (HttpWebRequest)WebRequest.Create(url);
            http_request_.Proxy = wpxy;

            SendUpdateEvent(Status.kConnecting, -1, -1);
            http_response_ = (HttpWebResponse)http_request_.GetResponse();
            SendUpdateEvent(Status.kConnected, -1, -1);

            //获取下载大小
            totalsize_ = http_response_.ContentLength;
            if(totalsize_ == -1)
                totalsize_ = 1;
            currentsize_ = 0;
            SendUpdateEvent(Status.kDownloading, currentsize_, totalsize_);
            
            if(md5_enable_)
            {
                md5 = MD5CryptoServiceProvider.Create();
                md5.Initialize();
            }

            Stream response_stream = http_response_.GetResponseStream();
            int readsize = 0;
            byte[] barr = new byte[BuffSize];

            while(!thread_stop_flag_)
            {
                timeout_timer.Start();
                readsize = response_stream.Read(barr, 0, BuffSize);
                timeout_timer.Stop();

                if(readsize == -1 || readsize == 0)
                    break;

                if(md5_enable_)
                    md5.TransformBlock(barr, 0, readsize, barr, 0);

                outstream_.Write(barr, 0, readsize);
                SendUpdateEvent(status_, currentsize_, totalsize_, CalcRemainTime());
                currentsize_ += readsize;
                status_ = Status.kDownloading;
            }

            if(md5_enable_)
            {
                md5.TransformFinalBlock(barr, 0, 0);
                HashToString(md5.Hash);
            }

            //关闭流
            outstream_.Flush();
            outstream_.Close();
        }
        catch(Exception ex)
        {
            status_ = Status.kFailed;
            SendUpdateEvent(ex.Message, status_, totalsize_, currentsize_);

            if(timeout_timer != null)
            {
                timeout_timer.Dispose();
                timeout_timer = null;
            }
            return;
        }
        finally
        {
            if(timeout_timer != null)
            {
                timeout_timer.Dispose();
                timeout_timer = null;
            }
            //设置结束时间
            end_time_ = DateTime.Now;
        }

        if(abort_flag_)
        {
            status_ = Status.kAborted;
            SendUpdateEvent(status_, totalsize_, currentsize_);
        }
        else
        {
            status_ = Status.kComplete;
            SendUpdateEvent(status_, totalsize_, currentsize_);
        }
    }

    private void SendUpdateEvent(Status status, long currentsize, long totalsize, TimeSpan span)
    {
        if(update_event != null)
            update_event(status.ToString(), status, currentsize, totalsize, span);
    }
    private void SendUpdateEvent(string message, Status status, long currentsize, long totalsize)
    {
        if(update_event != null)
            update_event(message, status, currentsize, totalsize, TimeSpan.Zero);
    }
    private void SendUpdateEvent(Status status, long currentsize, long totalsize)
    {
        SendUpdateEvent(status, currentsize, totalsize, TimeSpan.Zero);
    }
    private void StopTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        stop_timer_.Stop();
        try
        {
            try
            {
                outstream_.Close();
            }
            catch { }

            thread_.Abort();
            http_response_.Close();
            SendUpdateEvent(status_, currentsize_, totalsize_);
        }
        catch
        {
            SendUpdateEvent(status_, currentsize_, totalsize_);
        }
    }
    private void TimeoutTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        var timer = (System.Timers.Timer)sender;
        try
        {
            timer.Dispose();
            timer = null;

            try
            {
                outstream_.Close();
            }
            catch { }

            stop_timer_.Stop();
            
            thread_stop_flag_ = true;
            Thread.Sleep(200);

            status_ = Status.kTimeout;
            SendUpdateEvent(status_, currentsize_, totalsize_);
            thread_.Abort();
        }
        catch { }
    }
    private TimeSpan CalcRemainTime()
    {
        double total = totalsize_;
        double current = currentsize_;
        if(current < 0.001)
            current = 1;

        TimeSpan elapsed = DateTime.Now - start_time_;

        long total_ticks = (long)(elapsed.Ticks * (total / current));
        long remain_ticks = total_ticks - elapsed.Ticks;

        TimeSpan result = new TimeSpan(remain_ticks);
        return result;
    }
    private void HashToString(byte[] bytes)
    {
        md5_content_ = "";
        foreach(var b in bytes)
        {
            md5_content_ += string.Format("{0:X2}", b);
        }
    }

    /// <summary>
    /// 下载地址
    /// </summary>
    public string download_url
    {
        get { return download_url_; }
        set { download_url_ = value; }
    }
    private string download_url_;
    /// <summary>
    /// 更新事件
    /// </summary>
    public event UpdateDelegate update_event;
    /// <summary>
    /// 当前状态
    /// </summary>
    public Status status { get { return status_; } }
    private Status status_ = Status.kNone;
    /// <summary>
    /// 输出模式
    /// </summary>
    public OutputMode output_mode { get { return output_mode_; } }
    private OutputMode output_mode_ = OutputMode.kNone;
    /// <summary>
    /// 下载当前长度
    /// </summary>
    public long currentsize { get { return currentsize_; } }
    private long currentsize_ = 0;
    /// <summary>
    /// 下载总长度
    /// </summary>
    public long totalsize { get { return totalsize_; } }
    private long totalsize_ = 0;
    /// <summary>
    /// 超时设置
    /// </summary>
    public int time_out
    {
        get { return time_out_; }
        set 
        {
            if(status_ != Status.kNone)
                return;
            time_out_ = value;
        }
    }
    private int time_out_ = 5000;
    /// <summary>
    /// MD5计算是否启用，请在开始下载前设置，开始下载后将无法设置！
    /// </summary>
    public bool md5_enable 
    {
        get { return md5_enable_; }
        set 
        {
            if(status_ != Status.kNone)
                return;
            md5_enable_ = value;
        }
    }
    private bool md5_enable_ = true;
    /// <summary>
    /// 下载完成后，下载内容的MD5值。未下载完成时为null。
    /// 如果md5_enable=false，调用时将抛出 MD5NoEnabledException
    /// </summary>
    public string md5_content 
    {
        get 
        { 
            if(!md5_enable)
                throw new MD5NoEnabledException();
            return md5_content_; 
        }
    }
    private string md5_content_;
    /// <summary>
    /// 开始时间，未开始则返回null
    /// </summary>
    public DateTime start_time { get { return start_time_; } }
    private DateTime start_time_;
    /// <summary>
    /// 结束时间，未结束则返回null
    /// </summary>
    public DateTime end_time { get { return end_time_; } }
    private DateTime end_time_;
    /// <summary>
    /// 下载保存文件
    /// 注意与自定义输出流OutStream互斥
    /// </summary>
    public string savefilepath
    {
        set
        {
            //直接保存文件
            savefilepath_ = value;
            //自动创建文件流
            outstream_ = null;
            output_mode_ = OutputMode.kFile;
        }
        get { return savefilepath_; }
    }
    private string savefilepath_ = null;
    /// <summary>
    /// 自定义输出流
    /// 注意与下载文件LocalFilePath互斥
    /// </summary>
    public Stream outstream
    {
        set
        {
            //自定义流
            outstream_ = value;
            savefilepath_ = null;
            output_mode_ = OutputMode.kStream;
        }
        get { return outstream_; }
    }
    private Stream outstream_;

    /// <summary>
    /// 工作线程
    /// </summary>
    private Thread thread_; 
    /// <summary>
    /// Http Request
    /// </summary>
    private HttpWebRequest http_request_;
    /// <summary>
    /// Http Response
    /// </summary>
    private HttpWebResponse http_response_;
    /// <summary>
    /// 中断标记
    /// </summary>
    private bool abort_flag_ = false;
    /// <summary>
    /// 线程停止标记
    /// </summary>
    private bool thread_stop_flag_ = false;
    /// <summary>
    /// 停止计时器
    /// </summary>
    private System.Timers.Timer stop_timer_;
    
    
    
    
    
    
}
