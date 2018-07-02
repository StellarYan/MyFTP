using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace FTPClient
{
    




    class FTPClient
    {
        TcpClient client;
        NetworkStream stream;
        

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
                SendMessageToServer(userCommand.ToString() + MyFTPHelper.FTPNewLine);
                SendMessageToServer(passwordCommand.ToString() + MyFTPHelper.FTPNewLine);
            }
            catch(Exception exc)
            {
                PostMessageToConsoleWithLock(exc.ToString());
            }
        }

        public void StartListen()
        {
            PostMessageToConsoleWithLock("开始监听");
            while(true)
            {

            }
        }

    }
}
