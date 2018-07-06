using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.IO;
namespace FTPClient
{
    class FTPClient
    {
        int controlPort;
        int dataPort;

        TcpListener dataPortListener;

        TcpClient controlClient;
        NetworkStream controlStream;
        TcpClient dataClient;
        FileTransfer currenttransfer;

        public List<string> fileList;
        public DirectoryInfo downloadDirectory;
        public event EventHandler serverDisconnectEvent;

        static Queue<FTPReply> CachedReply = new Queue<FTPReply>();
        FTPReply? ReadNextReply()
        {
            if (CachedReply.Count > 0) return CachedReply.Dequeue();
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
            Array.ForEach(messages, (m) => CachedReply.Enqueue(FTPReply.String2Reply(m)));
            if (CachedReply.Count > 0) return CachedReply.Dequeue();
            else return null;
        }
        
        public event Action<string> ConsoleLogEvent;
        public void PostMessageToConsoleWithLock(string s)
        {
            lock(ConsoleLogEvent)
            {
                ConsoleLogEvent(s+Environment.NewLine);
            }
        }


        bool SendMessageToServerAndWaitReply(string msg)
        {
            try
            {
                MyFTPHelper.WriteToNetStream(msg, controlStream);
                ReadServerReply();
                return true;
            }
            catch(Exception exc)
            {
                if(exc.GetType()==typeof(IOException))
                {
                    serverDisconnectEvent(this, new EventArgs());
                }
                PostMessageToConsoleWithLock(exc.Message);
                return false;
            }
        }

        public void UpdateList()
        {
            FTPCommand fileListCommand = new FTPCommand("LIST", null);
            SendMessageToServerAndWaitReply(fileListCommand.ToString() + MyFTPHelper.FTPNewLine);
        }


        public void DownLoadFile(string filename,int size)
        {
            FTPCommand portCommand = new FTPCommand("PORT", new string[] { controlPort.ToString() });
            if (!SendMessageToServerAndWaitReply(portCommand.ToString() + MyFTPHelper.FTPNewLine)) return;
            ListenDataPort();
            PostMessageToConsoleWithLock("开始下载"+filename);
            FTPCommand downloadCommand = new FTPCommand("RETR", new string[] { filename });
            if (!SendMessageToServerAndWaitReply(downloadCommand.ToString() + MyFTPHelper.FTPNewLine)) return;

            try
            {
                FileStream fs = File.OpenWrite(downloadDirectory + filename);
                NetworkStream dataStream = dataClient.GetStream();
                currenttransfer = new FileTransfer()
                {
                    networkStream = dataStream,
                    filestream = fs,
                };
                currenttransfer.DownloadAsync(() =>
                {
                    fs.Close();
                    dataStream.Close();
                    PostMessageToConsoleWithLock("完成下载");
                });
            }
            catch(Exception exc)
            {
                PostMessageToConsoleWithLock(exc.Message);
            }


        }

        public void UploadFile(string filepath)
        {
            FTPCommand portCommand = new FTPCommand("PORT", new string[] { controlPort.ToString() });
            if (!SendMessageToServerAndWaitReply(portCommand.ToString() + MyFTPHelper.FTPNewLine)) return;
            ListenDataPort();

            PostMessageToConsoleWithLock("开始上传" + Path.GetFileName(filepath));
            FTPCommand uploadCommand = new FTPCommand("STOR", new string[] { Path.GetFileName(Path.GetFileName(filepath)) });
            if (!SendMessageToServerAndWaitReply(uploadCommand.ToString() + MyFTPHelper.FTPNewLine)) return;
            try
            {
                FileStream fs = File.OpenRead(filepath);
                NetworkStream dataStream = dataClient.GetStream();
                currenttransfer = new FileTransfer()
                {
                    networkStream = dataStream,
                    filestream = fs,
                };
                currenttransfer.UploadAsync(() =>
                {
                    fs.Close();
                    dataStream.Close();
                    PostMessageToConsoleWithLock("完成上传");
                });
            }
            catch (Exception exc)
            {
                PostMessageToConsoleWithLock(exc.Message);
            }


        }



        public FTPClient(IPAddress serverIP,int localControlPort,string user,string password, Action<string> ConsoleLogDelegate)
        {
            controlClient = new TcpClient();
            dataClient = new TcpClient();
            ConsoleLogEvent += ConsoleLogDelegate;
            controlPort = localControlPort;
            dataPort = controlPort + 1;
            try
            {
                controlClient.Connect(serverIP, MyFTPHelper.ftpControlPort);
                controlStream = controlClient.GetStream();

                FTPCommand userCommand = new FTPCommand("USER", new string[] { user });
                if (!SendMessageToServerAndWaitReply(userCommand.ToString() + MyFTPHelper.FTPNewLine)) return;
                FTPCommand passwordCommand = new FTPCommand("PASS", new string[] { password });
                if (!SendMessageToServerAndWaitReply(passwordCommand.ToString() + MyFTPHelper.FTPNewLine)) return;
            }
            catch(Exception exc)
            {
                throw exc;
            }
            
        }

        public void ListenDataPort()
        {
            if(dataPortListener ==null) dataPortListener = TcpListener.Create(dataPort);
            dataPortListener.Start();
            dataClient = dataPortListener.AcceptTcpClient();
            PostMessageToConsoleWithLock("已建立数据端口连接"+dataClient);
        }


        public void ReadServerReply()
        {
            while (true)
            {
                try
                {
                    FTPReply? nreply = ReadNextReply();
                    if (nreply == null) continue;
                    else
                    {
                        FTPReply reply = nreply.Value;
                        PostMessageToConsoleWithLock("服务器返回值:" + reply.replyCode);
                        switch (reply.replyCode)
                        {
                            case FTPReply.Code_FileList:
                                List<string> fileList = MyFTPHelper.DecodeFileList(reply.post);
                                this.fileList = fileList;
                                PostMessageToConsoleWithLock("更新服务器文件目录");
                                break;
                            case FTPReply.Code_UserNotLogIn:
                                PostMessageToConsoleWithLock("由于账号未登录，命令无效");
                                break;
                        }
                        break;
                    }
                }
                catch (Exception exc)
                {
                    throw exc;
                }

            }
        }
    }
        
}
