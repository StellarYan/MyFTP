using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Diagnostics;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;

namespace FTPClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        FTPClient client;
        public MainWindow()
        {
            InitializeComponent();
            this.Closed += (object sender, EventArgs e) => { Process.GetCurrentProcess().Kill(); };
        }

        private void ConnectServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                client = new FTPClient(IPAddress.Parse(ServerIP.Text),int.Parse(ControlPort.Text), Username.Text, Password.Text, (s) =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        ClientConsole.Text += s;
                    }
                    );
                });
                client.downloadDirectory = new DirectoryInfo(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    );
                ConnectGrid.IsEnabled = false;
                UploadGrid.IsEnabled = true;
                downloadGrid.IsEnabled = true;
                DownloadFile.IsEnabled = false;
                UploadFile.IsEnabled = false;
                client.serverDisconnectEvent += (s, a) =>
                  {
                      ConnectGrid.IsEnabled = true;
                      UploadGrid.IsEnabled = false;
                      downloadGrid.IsEnabled = false;
                  };
                UpdateView();
            }
            catch (Exception exc)
            {
                ClientConsole.Text += "连接失败" + exc.Message + Environment.NewLine;
            }
        }

        private void UpdateView()
        {
            if(client!=null)
            {
                if (client.fileList != null)
                {
                    ServerFileList.Items.Clear();
                    client.fileList.ForEach((f) => ServerFileList.Items.Add(f));
                }
                if (client.downloadDirectory != null)
                {
                    downloadDirectory.Text = client.downloadDirectory.ToString();
                    DownloadFile.IsEnabled = true;
                }
                if(UploadFilePath.Text!=String.Empty)
                {
                    UploadFile.IsEnabled = true;
                }
                
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            client.UpdateList();
            UpdateView();
        }

        private void SelectDownloadPath_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog FilePathDialog = new CommonOpenFileDialog();
            FilePathDialog.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            FilePathDialog.IsFolderPicker = true;
            FilePathDialog.Title = "选择下载目录";
            if (FilePathDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                downloadDirectory.Text = FilePathDialog.FileName + System.IO.Path.DirectorySeparatorChar;
            }
            if(client!=null)
            {
                client.downloadDirectory = new DirectoryInfo(downloadDirectory.Text);
            }
            UpdateView();
        }

        private void ClientConsole_TextChanged(object sender, TextChangedEventArgs e)
        {
            ClientConsole.ScrollToEnd();
        }

        private void DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            string[] ss = ServerFileList.SelectedItems[0].ToString().Split(' ');
            string filename = ss[0];
            string fileSize = ss[1];
            client.DownLoadFile(filename, int.Parse(fileSize));
        }

        private void SelectUploadFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog FilePathDialog = new CommonOpenFileDialog();
            FilePathDialog.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            FilePathDialog.Title = "选择上传文件";
            if (FilePathDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                UploadFilePath.Text = FilePathDialog.FileName;
            }
            UpdateView();

        }

        private void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            
            if (System.IO.File.Exists(UploadFilePath.Text.ToString()))
            {
                client.UploadFile(UploadFilePath.Text.ToString());
            }
            else
            {
                client.PostMessageToConsoleWithLock("不是合法的上传文件，请检查上传文件路径是否正确");
            }
            
        }
    }
}
