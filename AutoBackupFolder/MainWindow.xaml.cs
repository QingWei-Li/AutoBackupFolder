using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace AutoBackupFolder
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Forms.NotifyIcon notifyIcon;
        System.Timers.Timer aTimer = new System.Timers.Timer();
        static string SavePath;
        static string FolderPath;
        static int MaxFileCount;

        public MainWindow()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            InitNotifyIcon();
        }

        //托盘图标
        private void InitNotifyIcon()
        {
            this.notifyIcon = new System.Windows.Forms.NotifyIcon();
            this.notifyIcon.BalloonTipText = this.Title;
            this.notifyIcon.ShowBalloonTip(2000);
            this.notifyIcon.Text = this.Title;
            this.notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            this.notifyIcon.Visible = true;
            //打开菜单项
            System.Windows.Forms.MenuItem open = new System.Windows.Forms.MenuItem("Open");
            open.Click += new EventHandler(Show);

            //退出菜单项
            System.Windows.Forms.MenuItem exit = new System.Windows.Forms.MenuItem("Exit");
            exit.Click += new EventHandler(Close);
            //关联托盘控件
            System.Windows.Forms.MenuItem[] childen = new System.Windows.Forms.MenuItem[] { open, exit };
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu(childen);

            this.notifyIcon.MouseDoubleClick += notifyIcon_MouseDoubleClick;
        }

        void notifyIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (this.ShowInTaskbar)
                Hide(sender, e);

            else
                Show(sender, e);
        }

        private void Show(object sender, EventArgs e)
        {
            this.Visibility = System.Windows.Visibility.Visible;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private void Hide(object sender, EventArgs e)
        {
            this.ShowInTaskbar = false;
            this.Visibility = System.Windows.Visibility.Hidden;
        }

        private void Close(object sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        //引用resources内的dll
        System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string dllName = args.Name.Contains(",") ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name.Replace(".dll", "");
            dllName = dllName.Replace(".", "_");
            if (dllName.EndsWith("_resources")) return null;
            System.Resources.ResourceManager rm = new System.Resources.ResourceManager(GetType().Namespace + ".Properties.Resources", System.Reflection.Assembly.GetExecutingAssembly());
            byte[] bytes = (byte[])rm.GetObject(dllName);
            return System.Reflection.Assembly.Load(bytes);
        }

        private void tbPath_GotFocus(object sender, RoutedEventArgs e)
        {
            SelectFolderPath(sender);
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (tbFolderPath.Text.Length > 0 && tbSavePath.Text.Length > 0)
            {
                btnStop.IsEnabled = true;
                btnStart.IsEnabled = false;
                tbFolderPath.IsEnabled = false;
                tbSavePath.IsEnabled = false;
                sdInterval.IsEnabled = false;
                sdMaxFileCount.IsEnabled = false;

                SavePath = tbSavePath.Text;
                FolderPath = tbFolderPath.Text;
                MaxFileCount = Convert.ToInt32(sdMaxFileCount.Value);

                //先执行一次
                Thread thread = new Thread(BackupFolder);
                thread.Start();

                aTimer.Interval = Convert.ToInt32(sdInterval.Value) * 60 * 60 * 1000;
                aTimer.Elapsed += aTimer_Elapsed;
                aTimer.Start();
            }
            else
            {
                MessageBox.Show("请选择文件路径");
            }
        }
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            tbFolderPath.IsEnabled = true;
            tbSavePath.IsEnabled = true;
            sdInterval.IsEnabled = true;
            sdMaxFileCount.IsEnabled = true;
            aTimer.Dispose();
        }
        private void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
            sfd.Filter = "文本文件(*.txt)|*.txt";
            sfd.FileName = DateTime.Now.ToString("yy-MM-dd") + "Logs";
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    Stream s = File.OpenWrite(sfd.FileName);
                    using (StreamWriter sw = new StreamWriter(s))
                    {
                        sw.Write(tbLogs.Text);
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show(ex.Message, "错误信息", MessageBoxButton.OK);
                }
            }
        }

        void aTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            BackupFolder();
        }
        private static void SelectFolderPath(object sender)
        {
            TextBox tb = (TextBox)sender;
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.SelectedPath = System.Windows.Forms.Application.StartupPath;

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                tb.Text = fbd.SelectedPath + System.IO.Path.DirectorySeparatorChar;

        }
        private void BackupFolder()
        {
            //确保当前目录下的备份文件符合设置的最大数
            DirectoryInfo theFolder = new DirectoryInfo(SavePath);
            while (theFolder.GetFiles().Count() >= MaxFileCount)
            {
                FileInfo[] fileInfo = theFolder.GetFiles();
                fileInfo[0].Delete();
            }

            string savePath = SavePath + "FolderTemp" + System.IO.Path.DirectorySeparatorChar;
            string saveZip = SavePath + DateTime.Now.ToString("yyMMdd-HHmmss") + ".zip";

            //复制文件夹（因为文件可能是被占用状态，复制再处理就无视被占用的情况）
            CopyFolder(FolderPath, savePath);

            //压缩保存的文件夹
            ZipFloClass Zc = new ZipFloClass();
            Zc.ZipFile(savePath, saveZip);

            //删除复制的文件
            DirectoryInfo di = new DirectoryInfo(SavePath + "FolderTemp");
            di.Delete(true);

            //更新日志
            tbLogs.Dispatcher.Invoke(new Action(() => { tbLogs.Text += "[" + DateTime.Now.ToString() + "] " + saveZip + "\n"; }));
        }
        private static void CopyFolder(string from, string to)
        {
            if (!Directory.Exists(to))
                Directory.CreateDirectory(to);

            // 文件夹
            foreach (string sub in Directory.GetDirectories(from))
                CopyFolder(sub + "\\", to + System.IO.Path.GetFileName(sub) + "\\");

            // 文件
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, to + System.IO.Path.GetFileName(file), true);
        }

    }
}
