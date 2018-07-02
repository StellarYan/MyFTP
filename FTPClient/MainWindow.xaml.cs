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
                client = new FTPClient(IPAddress.Parse(ServerIP.Text), Username.Text, Password.Text, (s) =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        ClientConsole.Text += s;
                    }
                    );
                });
                client.downloadDirectory = new DirectoryInfo(downloadDirectoryLabel.Content.ToString());

            }
            catch (Exception exc)
            {
                ClientConsole.Text += "连接失败";
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
                    downloadDirectoryLabel.Content = client.downloadDirectory.ToString();
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
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
                downloadDirectoryLabel.Content = FilePathDialog.FileName + System.IO.Path.DirectorySeparatorChar;
            }
            if(client!=null)
            {
                client.downloadDirectory = new DirectoryInfo(downloadDirectoryLabel.Content.ToString());
            }
            UpdateView();
        }

        private void ClientConsole_TextChanged(object sender, TextChangedEventArgs e)
        {
            ClientConsole.ScrollToEnd();
        }
    }
}
