using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public static class MyFTPHelper
{
    public const int ftpControlPort = 21;
    public const int ftpDataPort = 20;
    const int commandBufferSize = 1024;
    public static string FTPNewLine = Environment.NewLine;
    public static string FileListNewLine = "\n";
    

    public static void WriteToNetStream(string s,NetworkStream stream)
    {
        try
        {
            byte[] buffer = Encoding.Unicode.GetBytes(s);
            stream.Write(buffer, 0, buffer.Length);
        }
        catch(Exception exc)
        {
            throw exc;
        }
    }

    public static string ReadFromNetStream(NetworkStream stream)
    {
        try
        {
            byte[] commandBuffer = new byte[commandBufferSize];
            int bytesCount = stream.Read(commandBuffer, 0, commandBufferSize);
            return Encoding.Unicode.GetString(commandBuffer, 0, bytesCount);
        }
        catch(Exception exc)
        {
            throw exc;
        }
    }


    public static string EncodeFileList(List<string> files)
    {
        string res = "";
        files.ForEach((s) => res += FileListNewLine + s);
        return res;
    }
    public static List<string> DecodeFileList(string s)
    {
        if (s == null) return new List<string>();
        var ss= s.Split(new string[] { FileListNewLine },StringSplitOptions.RemoveEmptyEntries);
        List<string> fileList = new List<string>();
        Array.ForEach(ss, (file) => fileList.Add(file));
        return fileList;
    }




}

static class FTPCommandProcessHelper
{
    static Queue<FTPCommand> CachedCommands = new Queue<FTPCommand>();
    public static FTPCommand? ReadNextCommand(NetworkStream stream)
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
}


public struct User
{
    public string username;
    public string password;
}





public struct FTPReply
{
    public const string Code_CommandNotImplemented = "502";
    public const string Code_UserLoggedIn = "230";
    public const string Code_UserNotLogIn = "530";
    public const string Code_FileList = "212";
    public const string Code_SyntaxErrorPara = "501";
    public const string Code_CommandOkay = "200 ";
    public const string Code_SyntaxError = "500";
    public const string Code_CantOopenDataConnection = "425";
    public const string Code_DataConnectionOpen = "225";
    public const string Code_ConnectionClosed = "426";
    public const string Code_TransferStarting = "125";



    public enum FirstReplyCode
    {
        PositivePreliminary=1,
        PositiveCompletion=2,
        PositiveIntermediate=3,
        TransientNegativeCompletion=4,
        PermanentNegativeCompletion=5
    }
    public enum SecondReplyCode
    {
        Syntax=0,
        Information=1,
        Connections=2,
        AuthenticationAndAccounting=3,
        Unspecified=4,
        FileSystem=5
    }
    FirstReplyCode firstCode
    {
        get { return (FirstReplyCode)(int)Char.GetNumericValue(replyCode[0]); }
    }
    SecondReplyCode secondCode
    {
        get { return (SecondReplyCode)(int)Char.GetNumericValue(replyCode[1]); }
    }
    int finerGradation
    {
        get { return (int)Char.GetNumericValue(replyCode[2]); }
    }
    public string replyCode;
    public string post;
    public static FTPReply String2Reply(string s)
    {
        string[] ss= s.Split(new char[] { ' ' }, 2,StringSplitOptions.RemoveEmptyEntries);
        return ss.Length == 2 ? new FTPReply() { replyCode = ss[0], post = ss[1] } : new FTPReply() { replyCode = ss[0], post = null };
    }
    public static string Reply2String(FTPReply reply)
    {
        return ((int)reply.firstCode).ToString() + ((int)reply.secondCode).ToString() + reply.finerGradation.ToString() + " " + reply.post;
    }

    public override string ToString()
    {
        return FTPReply.Reply2String(this);
    }

}

public struct FTPCommand
{
    public string controlCommand;
    public string[] parameters;
    public FTPCommand(string controlCommand,string[] parameters) 
    {
        this.controlCommand = controlCommand;
        this.parameters = parameters;
    }

    public static FTPCommand String2Command(string s)
    {
        string[] tokens = s.Split(' ');
        FTPCommand cmd = new FTPCommand();
        List<string> parameters = new List<string>();
        foreach (var token in tokens)
        {
            if (cmd.controlCommand == null) cmd.controlCommand = token;
            else parameters.Add(token);
        }
        cmd.parameters = parameters.ToArray();
        return cmd;
    }

    public static string Command2String(FTPCommand command)
    {
        String s = command.controlCommand;
        if(command.parameters!=null)
        {
            Array.ForEach(command.parameters, (p) => s += " " + p);
        }
        return s;
    }

    public override string ToString()
    {
        return Command2String(this);
    }

}

public enum TransferState
{
    Ready,Running,Finished,Error
}

/// <summary>
/// FTP标准中进行文件传输时，不需要在传输前指定文件大小。
/// 所以单纯的FTP中，服务器和客户端实际上到不知道文件是否传完了，只是随着流结束传输就结束了
/// </summary>
public class FileTransfer
{
    public TransferState State { get; private set; }
    public NetworkStream networkStream;
    public FileStream filestream;
    private int BytesTransfer;

    public const int BytebufferInitSize = 1024 * 1024;

    public void DownloadAsync(Action DownloadedCallback)
    {
        State = TransferState.Running;
        BytesTransfer = 0;
        Task.Run(() =>
        {
            List<byte> mbuffer = new List<byte>(BytebufferInitSize);
            try
            {
                while (true)
                {
                    int r = networkStream.ReadByte();
                    if (r < byte.MinValue || r > byte.MaxValue) break;
                    mbuffer.Add(Convert.ToByte(r));
                    BytesTransfer++;
                }
                filestream.Write(mbuffer.ToArray(), 0, BytesTransfer);
                State = TransferState.Finished;
                DownloadedCallback();

            }
            catch(Exception exc)
            {
                if(exc.GetType() == typeof(ObjectDisposedException))
                {
                    filestream.Write(mbuffer.ToArray(), 0, BytesTransfer);
                    State = TransferState.Finished;
                    DownloadedCallback();
                }
                else
                {
                    State = TransferState.Error;
                    throw exc;
                }
            }
            
        });
        return;
    }

    public void UploadAsync(Action UploadedCallback)
    {
        State = TransferState.Running;
        BytesTransfer = 0;
        Task.Run(() =>
        {
            try
            {
                List<byte> mbuffer = new List<byte>(BytebufferInitSize);
                while (true)
                {
                    int r = filestream.ReadByte();
                    if (r == -1) break;
                    mbuffer.Add(Convert.ToByte(r));
                    BytesTransfer++;
                }
                networkStream.Write(mbuffer.ToArray(), 0, BytesTransfer);
                State = TransferState.Finished;
                UploadedCallback();
            }
            catch (Exception exc)
            {
                State = TransferState.Error;
                throw exc;
            }

        });
        return;

    }
    

}
