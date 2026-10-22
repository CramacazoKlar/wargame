using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using UnityEditor;
namespace CampaignDemo.Editor {
 public static class TlsVerification {
  public static async void Run(){var log=new StringBuilder();int code=1;try{for(int i=0;i<2;i++){using(var c=new TcpClient()){await c.ConnectAsync("game.klarnote.com.cn",7788);using(var s=new SslStream(c.GetStream(),false,(sender,cert,chain,errors)=>{log.AppendLine("Certificate: "+cert.Subject+" issuer="+cert.Issuer+" errors="+errors);if(chain!=null)foreach(var status in chain.ChainStatus)log.AppendLine(status.Status+" "+status.StatusInformation);return errors==SslPolicyErrors.None;})){try{if(i==0)await s.AuthenticateAsClientAsync("game.klarnote.com.cn");else await s.AuthenticateAsClientAsync("game.klarnote.com.cn",null,SslProtocols.Tls12,false);log.AppendLine("PASS variant "+i+" "+s.SslProtocol);code=0;}catch(Exception e){log.AppendLine("FAIL variant "+i+" "+e.ToString());}}}}}catch(Exception e){log.AppendLine(e.ToString());}File.WriteAllText(Environment.GetEnvironmentVariable("TLS_VERIFY_RESULT"),log.ToString());EditorApplication.Exit(code);}
 }
}
