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
        TcpClient client;
        NetworkStream stream;

        public List<string> fileList;
        public DirectoryInfo downloadDirectory;

        static Queue<FTPReply> CachedReply = new Queue<FTPReply>();
        FTPReply? ReadNextReply()
        {
            if (CachedReply.Count > 0) return CachedReply.Dequeue();
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
            Array.ForEach(messages, (m) => CachedReply.Enqueue(FTPReply.String2Reply(m)));
            if (CachedReply.Count > 0) return CachedReply.Dequeue();
            else return null;
        }
        

        public event Action<string> ConsoleLogEvent;
        void PostMessageToConsoleWithLock(string s)
        {
            lock(ConsoleLogEvent)
            {
                ConsoleLogEvent(s);
            }
        }


        void SendMessageToServer(string msg)
        {
            try
            {
                MyFTPHelper.WriteToNetStream(msg, stream);
            }
            catch(Exception exc)
            {
                throw exc;
            }
            
        }


        public FTPClient(IPAddress serverIP,string user,string password, Action<string> ConsoleLogDelegate)
        {
            client = new TcpClient();
            try
            {
                client.Connect(serverIP, MyFTPHelper.ftpControlPort);
                stream = client.GetStream();

                FTPCommand userCommand = new FTPCommand("USER", new string[] { user });
                FTPCommand passwordCommand = new FTPCommand("PASS", new string[] { password });
                FTPCommand fileListCommand = new FTPCommand("LIST", null);
                SendMessageToServer(userCommand.ToString() + MyFTPHelper.FTPNewLine);
                SendMessageToServer(passwordCommand.ToString() + MyFTPHelper.FTPNewLine);
                SendMessageToServer(fileListCommand.ToString() + MyFTPHelper.FTPNewLine);

                ConsoleLogEvent += ConsoleLogDelegate;
            }
            catch(Exception exc)
            {
                PostMessageToConsoleWithLock(exc.ToString());
            }
            Thread t = new Thread(Start);
            t.Start();
        }

        public void Start()
        {
            PostMessageToConsoleWithLock("开始监听");
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
