using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Windows.Threading;




/*FTP的最小实现
 * ACCT,CWD,CDUP,SMNT,REIN,STOU,APPE,ALLO,RNFR,RNTO,DELE,RMD,MKD,PWD
 * NLST,SITE,SYST,HELP
 * 上面这些命令都不实现
 * 
 * 
 * REST 断点续传先不实现
 * STRU暂不实现，默认为文件结构为 file-structure，即连续序列字节即构成文件
 * MODE不实现，默认为Stream模式
 * TYPE不实现，默认为ASCII Non-print
 * 
 * PASV 让服务器监听指定端口
 * PORT 让服务器连接客户端的指定端口
 * //暂时只实现DATA PORT传输
 * 
 * 实现的命令
 * USER 指定账号
 * PASS 指定密码
 * LIST 回传当前文件目录
 * RETR 下载文件
 * STOR 上传文件
 * QUIT 关闭与服务器的连接
 * ABOR 放弃之前的文件传输
 * STAT 回传当前FTP状态
 * NOOP 服务器返回OK
 * 
 * 
 */

namespace FTPServer
{
    
    class FTPConnect
    {
        public ServerConnectionDispatcher serverDispatcher;
        public TcpClient client;
        NetworkStream stream;
        static Queue<FTPCommand> CachedCommands = new Queue<FTPCommand>();
        public User user = new User();
        public bool Logined=false;


        public FTPCommand? ReadNextCommand()
        {
            if (CachedCommands.Count > 0) return CachedCommands.Dequeue();
            string streamstring = null;
            try
            {
                streamstring = MyFTPHelper.ReadFromNetStream(stream);
            }
            catch (Exception exc)
            {
                throw exc;
            }
            string[] messages = streamstring.Split(new string[] { MyFTPHelper.FTPNewLine }, StringSplitOptions.RemoveEmptyEntries);
            Array.ForEach(messages, (m) => CachedCommands.Enqueue(FTPCommand.String2Command(m)));
            if (CachedCommands.Count > 0) return CachedCommands.Dequeue();
            else return null;
        }

        public FTPConnect(TcpClient t, ServerConnectionDispatcher serverDispatcher)
        {
            this.client = t;
            stream = t.GetStream();
            Thread thread = new Thread(this.Start);
            thread.Start();
            this.serverDispatcher = serverDispatcher;
        }
        public void Start()
        {
            while (true)
            {
                try
                {
                    FTPCommand? ncommand = ReadNextCommand();
                    if (ncommand == null) continue;
                    else
                    {
                        FTPCommand command = ncommand.Value;
                        serverDispatcher.PostMessageFromClient(command.controlCommand + "  " + string.Join(" ", command.parameters), this);
                        switch (command.controlCommand)
                        {
                            case "USER": //USER 指定账号
                                if(command.parameters.Length!=1)
                                {
                                    throw new Exception("用户名命令格式错误");
                                }
                                user.username = command.parameters[0];
                                break;
                            case "PASS": //PASS 指定密码
                                if (command.parameters.Length != 1)
                                {
                                    throw new Exception("密码命令格式错误");
                                }
                                user.password = command.parameters[0];
                                if(serverDispatcher.CheckUserWithLock(user))
                                {
                                    Logined = true;
                                    serverDispatcher.PostMessageFromClient("已成功登录",this);
                                    MyFTPHelper.WriteToNetStream(new FTPReply()
                                    {
                                        replyCode = FTPReply.Code_UserLoggedIn,
                                        post = "login success"
                                    }.ToString(), stream);
                                }
                                else
                                {
                                    serverDispatcher.PostMessageFromClient("密码或用户名有误",this);
                                    MyFTPHelper.WriteToNetStream(new FTPReply()
                                    {
                                        replyCode = FTPReply.Code_UserNotLogIn,
                                        post = "login fail"
                                    }.ToString(), stream);
                                }
                                break;
                            case "LIST": //LIST 返回服务器的文件目录（标准中不指定返回格式，格式为我们自定义）
                                MyFTPHelper.WriteToNetStream(
                                    new FTPReply()
                                    {
                                        replyCode = FTPReply.Code_FileList,
                                        post = serverDispatcher.GetEncodedFileList()
                                    }.ToString(),stream);
                                break;
                            case "PASV": //PASV 数据线程让服务器监听特定端口

                                break;
                            case "PORT": //PORT 数据线程让服务器连接客户端的指定端口
                                break;
                            case "RETR": //RETR 下载文件
                                break;
                            case "STOR": //STOR 上传文件
                                break;
                            case "ABOR": //QUIT 关闭与服务器的连接
                                break;
                            case "QUIT": //ABOR 放弃之前的文件传输
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch(System.IO.IOException exc)
                {
                    serverDispatcher.PostMessageFromClient(exc.Message, this);
                    client.Close();
                    stream.Close();
                    return;
                }
                
            }
        }
    }


    //用来隔离每个连接处理线程与主服务器线程之间的通信
    class ServerConnectionDispatcher
    {
        readonly FTPServer server;
        public ServerConnectionDispatcher(FTPServer server)
        {
            this.server = server;
        }

        public bool CheckUserWithLock(User user)
        {
            lock (server.registeredUsers)
            {
                return server.registeredUsers.Contains(user);
            }
        }

        public string GetEncodedFileList()
        {
            if (server.currentDirectory == null) return null;
            List<string> FileList = new List<string>();
            Array.ForEach(server.currentDirectory.GetFiles(), (f) =>
            {
                FileList.Add(f.Name + " " + f.Length + "byte");
            });
            return MyFTPHelper.EncodeFileList(FileList);
        }

        public void PostMessageFromClient(string msg, FTPConnect connect)
        {
            IPAddress ip = ((IPEndPoint)connect.client.Client.RemoteEndPoint).Address;
            if(connect.Logined)
                server.PostMessageToConsoleWithLock(DateTime.Now + " " + ip.ToString() +" "+ connect.user.username+"   " + msg);
            else
                server.PostMessageToConsoleWithLock(DateTime.Now + " " + ip.ToString() + "   " + msg);
        }
    }


    class FTPServer
    {
        TcpListener listener;
        List<FTPConnect> FTPConnects = new List<FTPConnect>();
        Thread listenThread;

        public DirectoryInfo currentDirectory;
        public List<User> registeredUsers;


        public event Action<string> ConsoleLogEvent;
        ServerConnectionDispatcher connectionDispatcher;

        public void PostMessageToConsoleWithLock(string s)
        {
            lock(ConsoleLogEvent)
            {
                ConsoleLogEvent(s);
            }
        }

        public FTPServer(string filePath,Action<string> ConsoleLogDelegate)
        {
            listener = TcpListener.Create(MyFTPHelper.ftpControlPort);
            ConsoleLogEvent += ConsoleLogDelegate;
            listener.Start();
            listenThread = new Thread(this.StartListen);
            listenThread.Start();
            connectionDispatcher = new ServerConnectionDispatcher(this);
        }

        void StartListen()
        {
            PostMessageToConsoleWithLock("开始监听");
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                FTPConnect connect = new FTPConnect(client, connectionDispatcher);
                FTPConnects.Add(connect);
                PostMessageToConsoleWithLock(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString() + "已连接");
            }
        }
    }
}
