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
        const int controlPort = 10033;
        const int dataPort = controlPort+1;



        TcpClient controlClient;
        NetworkStream controlStream;
        TcpClient dataClient;
        NetworkStream dataStream;
        FileTransfer currenttransfer;

        public List<string> fileList;
        public DirectoryInfo downloadDirectory;

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
        void PostMessageToConsoleWithLock(string s)
        {
            lock(ConsoleLogEvent)
            {
                ConsoleLogEvent(s+Environment.NewLine);
            }
        }


        void SendMessageToServer(string msg)
        {
            try
            {
                MyFTPHelper.WriteToNetStream(msg, controlStream);
            }
            catch(Exception exc)
            {
                throw exc;
            }
            
        }


        public void DownLoadFile(string filename,int size)
        {
            PostMessageToConsoleWithLock("开始下载"+filename);
            FTPCommand downloadCommand = new FTPCommand("RETR", new string[] { filename });
            SendMessageToServer(downloadCommand.ToString() + MyFTPHelper.FTPNewLine);
            FileStream fs = File.OpenWrite(downloadDirectory + filename);
            currenttransfer = new FileTransfer()
            {
                networkStream = dataStream,
                filestream = fs,
                fileByteCount = size,  
            };
            currenttransfer.thread = new Thread(currenttransfer.Download);
            currenttransfer.thread.Start();
        }



        public FTPClient(IPAddress serverIP,string user,string password, Action<string> ConsoleLogDelegate)
        {
            controlClient = new TcpClient();
            dataClient = new TcpClient();
            

            try
            {
                controlClient.Connect(serverIP, MyFTPHelper.ftpControlPort);
                controlStream = controlClient.GetStream();
                FTPCommand portCommand = new FTPCommand("PORT", new string[] { controlPort.ToString() });
                FTPCommand userCommand = new FTPCommand("USER", new string[] { user });
                FTPCommand passwordCommand = new FTPCommand("PASS", new string[] { password });
                FTPCommand fileListCommand = new FTPCommand("LIST", null);
                SendMessageToServer(portCommand.ToString() + MyFTPHelper.FTPNewLine);
                SendMessageToServer(userCommand.ToString() + MyFTPHelper.FTPNewLine);
                SendMessageToServer(passwordCommand.ToString() + MyFTPHelper.FTPNewLine);
                SendMessageToServer(fileListCommand.ToString() + MyFTPHelper.FTPNewLine);

                ConsoleLogEvent += ConsoleLogDelegate;
            }
            catch(Exception exc)
            {
                PostMessageToConsoleWithLock(exc.ToString());
            }
            Thread listenthread = new Thread(ListenDataPort);
            listenthread.Start();
            Thread t = new Thread(Start);
            t.Start();
        }

        public void ListenDataPort()
        {
            var listener = TcpListener.Create(dataPort);
            listener.Start();
            TcpClient dataClient = listener.AcceptTcpClient();
            dataStream = dataClient.GetStream();
            PostMessageToConsoleWithLock("已建立数据端口连接");
        }
        

        public void Start()
        {
            
            while(true)
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
                        }
                    }
                }
                catch(Exception exc)
                {
                    PostMessageToConsoleWithLock(exc.Message);
                    return;
                }
                
            }
        }

    }
}
