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
        public TcpClient controlClient;
        NetworkStream controlStream;
        TcpClient dataClient;
        FileTransfer currentTransfer;
        static Queue<FTPCommand> CachedCommands = new Queue<FTPCommand>();
        public User user = new User();
        public bool Logined=false;
        public int controlPort = 0;

        

        public FTPCommand? ReadNextCommand()
        {
            if (CachedCommands.Count > 0) return CachedCommands.Dequeue();
            string streamstring = null;
            try
            {
                streamstring = MyFTPHelper.ReadFromNetStream(controlStream);
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
            this.controlClient = t;
            controlStream = t.GetStream();
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
                        FTPReply reply = new FTPReply() { replyCode=FTPReply.Code_SyntaxError };
                        switch (command.controlCommand)
                        {
                            case "USER": //USER 指定账号
                                if (command.parameters.Length != 1)
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_SyntaxErrorPara, };
                                    break;
                                }
                                user.username = command.parameters[0];
                                break;
                            case "PASS": //PASS 指定密码
                                if (command.parameters.Length != 1)
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_SyntaxErrorPara, };
                                    break;
                                }
                                user.password = command.parameters[0];
                                if (serverDispatcher.CheckUserWithLock(user))
                                {
                                    Logined = true;
                                    serverDispatcher.PostMessageFromClient("已成功登录", this);
                                    reply = new FTPReply()
                                    {
                                        replyCode = FTPReply.Code_UserLoggedIn,
                                        post = "login success"
                                    };
                                }
                                else
                                {
                                    serverDispatcher.PostMessageFromClient("密码或用户名有误", this);
                                    reply = new FTPReply()
                                    {
                                        replyCode = FTPReply.Code_UserNotLogIn,
                                        post = "login fail"
                                    };
                                }
                                break;
                            case "LIST": //LIST 返回服务器的文件目录（标准中不指定返回格式，格式为我们自定义）
                                reply = new FTPReply()
                                {
                                    replyCode = FTPReply.Code_FileList,
                                    post = serverDispatcher.GetEncodedFileList()
                                };
                                break;
                            case "STOR": //STOR 客户端上传文件
                                if (command.parameters.Length != 1)
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_SyntaxErrorPara };
                                    break;
                                }
                                string filename = command.parameters[0];
                                FileStream downloadFileStream = File.OpenWrite(serverDispatcher.GetCurrentDirectory() + filename);
                                NetworkStream downloadDataStream = dataClient.GetStream();
                                if (downloadFileStream == null )
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_CantOopenDataConnection };
                                    break;
                                }
                                if (dataClient == null)
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_ConnectionClosed };
                                    break;
                                }
                                currentTransfer = new FileTransfer()
                                {
                                    networkStream = downloadDataStream,
                                    filestream = downloadFileStream,
                                };
                                currentTransfer.DownloadAsync(() =>
                                    {
                                        downloadDataStream.Close();
                                        downloadFileStream.Close();
                                        serverDispatcher.PostMessageFromClient("文件上传完成", this);
                                    }
                                );

                                reply = new FTPReply() { replyCode = FTPReply.Code_ConnectionClosed };
                                break;
                            case "RETR": //RETR 客户端下载文件
                                if (command.parameters.Length != 1)
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_SyntaxErrorPara };
                                    break;
                                }
                                FileStream uploadFileStream = serverDispatcher.OpenFileStreamInfileList(command.parameters[0]);
                                NetworkStream uploadDataStream = dataClient.GetStream();
                                if (uploadFileStream == null)
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_CantOopenDataConnection };
                                    break;
                                }
                                if (dataClient == null)
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_ConnectionClosed };
                                    break;
                                }
                                currentTransfer = new FileTransfer()
                                {
                                    networkStream = uploadDataStream,
                                    filestream = uploadFileStream,
                                };
                                currentTransfer.UploadAsync(() =>
                                    {
                                        uploadDataStream.Close();
                                        uploadFileStream.Close();
                                        serverDispatcher.PostMessageFromClient("文件上传完成", this);
                                    }
                                );

                                reply = new FTPReply() { replyCode = FTPReply.Code_ConnectionClosed };
                                break;
                            case "ABOR": //QUIT 关闭与服务器的连接
                                throw new NotImplementedException();
                            case "QUIT": //ABOR 放弃之前的文件传输
                                throw new NotImplementedException();
                            case "PASV": //PASV 数据线程让服务器监听特定端口
                                throw new NotImplementedException();
                            case "PORT": //PORT 客户端的控制端口为N，数据端口为N+1，服务器的控制端口为21，数据端口为20
                                if (command.parameters.Length != 1 ||  !int.TryParse(command.parameters[0],out controlPort) )
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_SyntaxErrorPara };
                                    break;
                                }
                                if(!serverDispatcher.CheckDataPortLegal(controlPort,this))
                                {
                                    reply = new FTPReply() { replyCode = FTPReply.Code_CantOopenDataConnection };
                                    break;
                                }
                                var remoteDataEnd = (IPEndPoint)controlClient.Client.RemoteEndPoint;
                                remoteDataEnd.Port = controlPort + 1;
                                dataClient = new TcpClient();
                                reply = new FTPReply() { replyCode = FTPReply.Code_DataConnectionOpen };
                                dataClient.ConnectAsync(remoteDataEnd.Address.MapToIPv4(), remoteDataEnd.Port);
                                serverDispatcher.PostMessageFromClient("与"+user.username+"建立数据连接", this);
                                break;
                            default:
                                break;
                        }
                        MyFTPHelper.WriteToNetStream(reply.ToString(), controlStream);
                    }
                }
                catch(System.IO.IOException exc)
                {
                    serverDispatcher.PostMessageFromClient(exc.Message, this);
                    controlClient.Close();
                    controlStream.Close();
                    return;
                }
                
            }
        }
    }


    //用来隔离每个连接处理线程与主服务器线程之间的通信
    class ServerConnectionDispatcher
    {
        readonly FTPServer server;

        public bool CheckDataPortLegal(int port,FTPConnect connect)
        {
            bool res = true;
            server.FTPConnects.ForEach((c) =>
            {
                if (c!= connect && ( port == c.controlPort || port == c.controlPort + 1 || port == c.controlPort - 1))
                    res = false;
            });
            return res;
            
        }
        

        public DirectoryInfo GetCurrentDirectory()
        {
            return server.currentDirectory;
        }
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

        public FileStream OpenFileStreamInfileList(string filename)
        {
            FileStream fs = null;
            Array.ForEach(server.currentDirectory.GetFiles(), (f) => { if (f.Name == filename) fs = f.OpenRead(); });
            return fs;
        }



        public string GetEncodedFileList()
        {
            if (server.currentDirectory == null) return null;
            List<string> FileList = new List<string>();
            Array.ForEach(server.currentDirectory.GetFiles(), (f) =>
            {
                FileList.Add(f.Name + " " + f.Length);
            });
            return MyFTPHelper.EncodeFileList(FileList);
        }

        public void PostMessageFromClient(string msg, FTPConnect connect)
        {
            IPAddress ip = ((IPEndPoint)connect.controlClient.Client.RemoteEndPoint).Address;
            
            if(connect.Logined)
                server.PostMessageToConsoleWithLock(DateTime.Now + " " + connect.user.username+"@"+ ip.MapToIPv4().ToString() +" " + msg);
            else
                server.PostMessageToConsoleWithLock(DateTime.Now + " " + ip.ToString() + "   " + msg);
        }
    }


    class FTPServer
    {
        TcpListener listener;
        public List<FTPConnect> FTPConnects = new List<FTPConnect>();
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
