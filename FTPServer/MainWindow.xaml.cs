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
using System.Diagnostics;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;


namespace FTPServer
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        FTPServer Server;

        DirectoryInfo downloadDirectory;
        List<User> registeredUsers;
        public MainWindow()
        {
            InitializeComponent();
            this.Closed += (object sender, EventArgs e) => { Process.GetCurrentProcess().Kill(); };
            registeredUsers = new List<User>();
            registeredUsers.Add(new User() { username = "TextBox", password = "TextBox" });
        }
        

        private void InitServer_Click(object sender, RoutedEventArgs e)
        {
            Server = new FTPServer(".",(s)=>this.Dispatcher.Invoke(() => ServerConsole.Text += (s + Environment.NewLine)));
            InitServer.IsEnabled = false;
            UpdateServer();
        }

        private void UpdateServer()
        {
            if(Server!=null)
            {
                Server.registeredUsers = registeredUsers;
                Server.currentDirectory = downloadDirectory;
            }
        }


        private void UpdateView()
        {
            if(registeredUsers!=null)
            {
                UserList.Items.Clear();
                registeredUsers.ForEach((u) => UserList.Items.Add("账号:" + u.username + " 密码:" + u.password));
            }
            if(downloadDirectory!=null)
            {
                FileListBox.Items.Clear();
                Array.ForEach(downloadDirectory.GetFiles(), (f) =>
                 {
                     FileListBox.Items.Add(f.Name + " " + f.Length + "byte");
                 });
            }
        }

        private void SelectFilePath_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog FilePathDialog = new CommonOpenFileDialog();
            FilePathDialog.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            FilePathDialog.IsFolderPicker = true;
            FilePathDialog.Title = "选择FTP服务器文件夹";
            if(FilePathDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                SelectFilePathLabel.Content = FilePathDialog.FileName + System.IO.Path.DirectorySeparatorChar;
            }
            downloadDirectory = new DirectoryInfo(SelectFilePathLabel.Content.ToString());
            UpdateView();
            UpdateServer();
        }

        private void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            if(Username.Text!=string.Empty && Password.Text != string.Empty)
            {
                registeredUsers.Add(new User() { username = Username.Text, password = Password.Text });
            }
            Username.Text = string.Empty;
            Password.Text = string.Empty;
            UpdateView();
            UpdateServer();
        }

        private void ServerConsole_TextChanged(object sender, TextChangedEventArgs e)
        {
            ServerConsole.ScrollToEnd();
        }
    }
}
