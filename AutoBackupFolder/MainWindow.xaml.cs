using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

namespace AutoBackupFolder
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Timers.Timer aTimer = new System.Timers.Timer();
        static string SavePath;
        static string FolderPath;
        static int MaxFileCount;

        public MainWindow()
        {
            InitializeComponent();
        }

        private static void SelectFolderPath(object sender)
        {
            TextBox tb = (TextBox)sender;
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            fbd.ShowDialog();
            if (fbd.SelectedPath != string.Empty)
                tb.Text = fbd.SelectedPath + System.IO.Path.DirectorySeparatorChar;
        }

        private void tbPath_GotFocus(object sender, RoutedEventArgs e)
        {
            SelectFolderPath(sender);
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (tbFolderPath.Text.Length > 0 || tbSavePath.Text.Length > 0)
            {
                btnStop.IsEnabled = true;
                btnStart.IsEnabled = false;

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
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            aTimer.Dispose();
        }

        void aTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DirectoryInfo theFolder = new DirectoryInfo(SavePath);
            if (theFolder.GetFiles().Count() >= MaxFileCount)
            {
                FileInfo[] fileInfo = theFolder.GetFiles();
                fileInfo[0].Delete();
            }

            BackupFolder();
        }

        private void BackupFolder()
        {
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

    }
}
