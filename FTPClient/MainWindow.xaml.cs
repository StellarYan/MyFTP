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
                        ClientConsole.Content += s;
                    }
                    );
                });
            }
            catch (Exception exc)
            {
                ClientConsole.Content += "连接失败";
            }
        }
    }
}
