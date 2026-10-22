using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
namespace CampaignDemo {
 [Serializable] public sealed class LobbyRequest {public string op,username,password,token,code,ticket;}
 [Serializable] public sealed class LobbyReply {public bool ok;public string error,token,code,ticket,role;}
 public static class InternetLobby {
  public static string Token,Code,Ticket,Role;
  public static bool InBattle;
  public static string Host="127.0.0.1";public static int Port=7788;
  public static bool Local {get{return Host=="127.0.0.1"||Host=="localhost";}}
  public static async Task<Tuple<TcpClient,Stream>> Connect(){var c=new TcpClient();try{var t=c.ConnectAsync(Host,Port);if(await Task.WhenAny(t,Task.Delay(8000))!=t)throw new TimeoutException("连接服务器超时");await t;c.NoDelay=true;Stream stream=c.GetStream();if(!Local){var ssl=new SslStream(stream,false);var auth=ssl.AuthenticateAsClientAsync(Host);if(await Task.WhenAny(auth,Task.Delay(8000))!=auth)throw new TimeoutException("安全连接超时");await auth;stream=ssl;}return Tuple.Create(c,stream);}catch(Exception e){c.Close();Debug.LogError("游戏安全连接失败："+e.ToString());throw;}}
  public static async Task Write(Stream s,string text){var b=Encoding.UTF8.GetBytes(text);int n=b.Length;await s.WriteAsync(new byte[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n},0,4);await s.WriteAsync(b,0,n);}
  static async Task ReadExactly(Stream s,byte[] b){int at=0;while(at<b.Length){int n=await s.ReadAsync(b,at,b.Length-at);if(n==0)throw new IOException("服务器已断开");at+=n;}}
  public static async Task<string> Read(Stream s){var h=new byte[4];await ReadExactly(s,h);int n=(h[0]<<24)|(h[1]<<16)|(h[2]<<8)|h[3];if(n<=0||n>4096)throw new IOException("服务器回复无效");var b=new byte[n];await ReadExactly(s,b);return Encoding.UTF8.GetString(b);}
  public static async Task<LobbyReply> Request(LobbyRequest request){request.token=Token;string json=JsonUtility.ToJson(request);var pair=await Connect();using(pair.Item1){await Write(pair.Item2,json);var read=Read(pair.Item2);if(await Task.WhenAny(read,Task.Delay(10000))!=read)throw new TimeoutException("服务器回复超时");var reply=JsonUtility.FromJson<LobbyReply>(await read);if(reply==null||!reply.ok)throw new IOException(reply==null?"回复无效":reply.error);return reply;}}
  public static async Task Leave(){string code=Code;Code=Ticket=Role=null;InBattle=false;if(code==null)return;try{await Request(new LobbyRequest{op="leave",code=code});}catch{}}
 }
}
