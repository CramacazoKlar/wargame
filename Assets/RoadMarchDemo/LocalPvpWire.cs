using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace CampaignDemo {
 // Length-prefixed UTF-8 frames. No Unity API runs on the socket threads.
 public sealed class LocalPvpWire:IDisposable {
  public const int MaximumFrame=4*1024*1024;
  readonly TcpClient client;readonly Stream stream;readonly ConcurrentQueue<string> incoming=new ConcurrentQueue<string>(),outgoing=new ConcurrentQueue<string>();readonly AutoResetEvent signal=new AutoResetEvent(false);
  volatile bool closed;string latestSnapshot;int receivedCount,queuedCount;
  public bool Closed {get{return closed;}}
  public string Error {get;private set;}
  public LocalPvpWire(TcpClient socket,Stream transport=null){client=socket;client.NoDelay=true;stream=transport??client.GetStream();Task.Run((Action)ReadLoop);Task.Run((Action)WriteLoop);}
  public bool Receive(out string text){if(incoming.TryDequeue(out text)){Interlocked.Decrement(ref receivedCount);return true;}return false;}
  public void Send(string text,bool snapshot=false){if(closed)return;if(snapshot)Interlocked.Exchange(ref latestSnapshot,text);else{if(Interlocked.Increment(ref queuedCount)>128){Dispose();return;}outgoing.Enqueue(text);}signal.Set();}
  void ReadLoop(){try{var header=new byte[4];while(!closed){ReadExactly(header);int length=(header[0]<<24)|(header[1]<<16)|(header[2]<<8)|header[3];if(length<=0||length>MaximumFrame)throw new IOException("网络消息大小无效");var body=new byte[length];ReadExactly(body);if(Interlocked.Increment(ref receivedCount)>128)throw new IOException("网络消息过多");incoming.Enqueue(Encoding.UTF8.GetString(body));}}catch(Exception e){if(!closed)Error=e.Message;}finally{Dispose();}}
  void ReadExactly(byte[] data){int at=0;while(at<data.Length){int count=stream.Read(data,at,data.Length-at);if(count==0)throw new EndOfStreamException("连接已断开");at+=count;}}
  void WriteLoop(){try{while(!closed){string text;if(outgoing.TryDequeue(out text))Interlocked.Decrement(ref queuedCount);else{text=Interlocked.Exchange(ref latestSnapshot,null);if(text==null){signal.WaitOne(200);continue;}}var body=Encoding.UTF8.GetBytes(text);if(body.Length>MaximumFrame)throw new IOException("网络消息太大");int n=body.Length;var header=new byte[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n};stream.Write(header,0,4);stream.Write(body,0,n);}}catch(Exception e){if(!closed)Error=e.Message;}finally{Dispose();}}
  public void Dispose(){closed=true;try{client.Close();}catch{}signal.Set();}
 }
}
