using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Timers;
using Utility.ModifyRegistry;
using System.IO;
using System.Collections.Specialized;
using ICSharpCode.SharpZipLib.Zip;
using System.Net;

namespace JaxUploader
{
    public partial class Main : Form
    {
        //delegate void updateStatusStripTextCallback(string text);
        delegate void updatePathInputCallback(string text);
        delegate void updatelogFilesDataViewCallback(string text, bool uploaded);
        delegate void clearlogFilesDataViewCallback();
        delegate void updateLogListCallback(DataGridViewRow row);
        delegate void updateMainLogCallback(string text);

        string processName = "LolClient";
        //string processName = "Notepad";
        string clientPath = "";

        StringCollection logs = new StringCollection();

        // How often do we want to start the background worker
        // to check to see if LoL is running
        int processTimerDelay = 750;
        System.Timers.Timer processTimer = new System.Timers.Timer();

        public Main()
        {
            InitializeComponent();

            if (JaxUploader.Properties.Settings.Default.UpgradeRequired)
            {
                JaxUploader.Properties.Settings.Default.Upgrade();
                JaxUploader.Properties.Settings.Default.UpgradeRequired = false;
            }

            logs = JaxUploader.Properties.Settings.Default.UploadedLogs;

            addToLog("Client v" + Application.ProductVersion + " started!");

            // Set the icon for the notify area and main form
            this.notifyIcon.Icon = Properties.Resources.icon;
            this.Icon = Properties.Resources.icon;

            fetchClientPath();

            addToLog("Process scanning started for lolclient.exe");
            //updateStatusStripText("Scanning for League of Legends client...");

            startProcessTimer();
        }

        // Run the worked start every time elapse
        public void startProcessTimer()
        {
            processTimer.Elapsed += new ElapsedEventHandler(processChecker_Start);
            processTimer.Interval = processTimerDelay;
            processTimer.Start();
        }

        // Only run the worker if it isn't busy (ie. it isnt waiting for LoL to close)
        public void processChecker_Start(object source, ElapsedEventArgs e)
        {
            if (!this.processChecker.IsBusy)
            {
                this.processChecker.RunWorkerAsync();
            }
        }
        
        // Check to see if LoL is running, if it is, attach event
        private void processChecker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (Process.GetProcessesByName(processName).Length > 0)
            {
                addToLog("Client found! Waiting for exit...");
                //updateStatusStripText("Client found! Waiting for exit...");

                Process[] lolclients = Process.GetProcessesByName(processName);
                Process myProcesses = lolclients[0];

                myProcesses.EnableRaisingEvents = true;
                myProcesses.Exited += new EventHandler(myProcesses_Exited);
                myProcesses.WaitForExit();
            }
        }

        // Run when LoL is exited
        private void myProcesses_Exited(object sender, System.EventArgs e)
        {
            addToLog("Process lolclient.exe exited");
            //updateStatusStripText("Client exited! Checking log files...");
            refreshLogFiles();
            StringCollection files = getFilesToUpload();
            if (files.Count > 0)
            {
                //updateStatusStripText("Found " + files.Count.ToString() + " logs to upload, processing...");
                addToLog("Found " + files.Count.ToString() + " logs to upload");
                createZip(files);
            }
            else
            {
                addToLog("No new logs found");
                //updateStatusStripText("No new log files found");
            }
            addToLog("Process scanning started for lolclient.exe");
        }

        private void createZip(StringCollection files)
        {
            addToLog("Compressing log files...");
            ZipFile z = ZipFile.Create("upload.zip");
            z.BeginUpdate();

            foreach (String file in files)
            {
                z.Add(clientPath + "/Air/logs/" + file, file);
            }
            z.CommitUpdate();
            z.Close();

            //updateStatusStripText("Files processed, starting upload...");
            sendZipToServer();
            markFilesUploaded(files);
        }

        private void sendZipToServer()
        {
            addToLog("Sending files to lolbase.net...");
            CookieContainer cookies = new CookieContainer();
            NameValueCollection querystring = new NameValueCollection();
            string outdata = UploadFileEx("upload.zip", "http://www.lolbase.net/upload/app/d3fd0722cf163280090c404c98eef83f", "logfile", "application/zip", querystring, cookies);
            //updateStatusStripText("Upload complete!");
            addToLog("Upload complete!");
        }

        private void markFilesUploaded(StringCollection files)
        {
            foreach (DataGridViewRow row in this.logFilesDataView.Rows)
            {
                if (files.Contains(row.Cells[0].Value.ToString()))
                {
                    updateLogList(row);
                    logs.Add(row.Cells[0].Value.ToString());
                }
            }
        }

        //private void updateStatusStripText(string text)
        //{
        //    if (this.statusStrip.InvokeRequired)
        //    {
        //        updateStatusStripTextCallback d = new updateStatusStripTextCallback(updateStatusStripText);
        //        this.Invoke(d, new object[] { text });
        //    }
        //    else
        //    {
        //        this.toolStripStatus.Text = text;
        //    }
        //}

        //private void updatePathInput(string text)
        //{
        //    if (this.pathDisabledInput.InvokeRequired)
        //    {
        //        updatePathInputCallback d = new updatePathInputCallback(updatePathInput);
        //        this.Invoke(d, new object[] { text });
        //    }
        //    else
        //    {
        //        this.pathDisabledInput.Text = text;
        //    }
        //}

        private void addToLog(string text)
        {
            if (this.mainLog.InvokeRequired)
            {
                updateMainLogCallback d = new updateMainLogCallback(addToLog);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                string timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                this.mainLog.AppendText("[" + timestamp + "]  " + text + "\n");
            }
        }

        private void addToLogList(string text, bool uploaded)
        {
            if (this.logFilesDataView.InvokeRequired)
            {
                updatelogFilesDataViewCallback d = new updatelogFilesDataViewCallback(addToLogList);
                this.Invoke(d, new object[] { text, uploaded });
            }
            else
            {
                this.logFilesDataView.Rows.Add(text, uploaded);
            }
        }

        private void updateLogList(DataGridViewRow row)
        {
            if (this.logFilesDataView.InvokeRequired)
            {
                updateLogListCallback d = new updateLogListCallback(updateLogList);
                this.Invoke(d, new object[] { row });
            }
            else
            {
                row.Cells[1].Value = true;
            }
        }

        private void clearlogFilesDataView()
        {
            if (this.logFilesDataView.InvokeRequired)
            {
                clearlogFilesDataViewCallback d = new clearlogFilesDataViewCallback(clearlogFilesDataView);
                this.Invoke(d, new object[] { });
            }
            else
            {
                this.logFilesDataView.Rows.Clear();
            }
        }

        private void changePathButton_Click(object sender, EventArgs e)
        {
            pathFolderBrowser.ShowDialog();
            updateClientPath(pathFolderBrowser.SelectedPath);
        }

        private string updateClientPath(string path)
        {
            clientPath = path;
            //updatePathInput(clientPath);
            ModifyRegistry myRegistry = new ModifyRegistry();
            myRegistry.SubKey = "Software\\JaxUploader";
            myRegistry.Write("lolpath", path);
            addToLog("LoL path set to " + clientPath);
            refreshLogFiles();
            return path;
        }

        private void fetchClientPath()
        {
            ModifyRegistry myRegistry = new ModifyRegistry();
            myRegistry.SubKey = "Software\\LoLBase";
            string officialPath = myRegistry.Read("lolpath");

            myRegistry.SubKey = "Software\\JaxUploader";
            string jaxPath = myRegistry.Read("lolpath");

            if (jaxPath != null)
            {
                clientPath = jaxPath;
            }
            else if (officialPath != null)
            {
                clientPath = officialPath;
            }
            else
            {
                clientPath = changePathButton_Click();
            }

            updateClientPath(clientPath);
        }

        private string changePathButton_Click()
        {
            throw new NotImplementedException();
        }

        private void refreshLogFiles()
        {
            addToLog("Refreshing log files...");
            clearlogFilesDataView();

            try
            {
                DirectoryInfo di = new DirectoryInfo(clientPath + "/Air/logs");
                FileInfo[] rgFiles = di.GetFiles("*.log");

                foreach (FileInfo fi in rgFiles)
                {
                    if (logs.Contains(fi.Name))
                    {
                        addToLogList(fi.Name, true);
                    }
                    else
                    {
                        addToLogList(fi.Name, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private StringCollection getFilesToUpload()
        {
            StringCollection files = new StringCollection();

            foreach (DataGridViewRow row in this.logFilesDataView.Rows)
            {
                if (row.Cells[1].Value.ToString() == "False")
                {
                    files.Add(row.Cells[0].Value.ToString());
                    addToLog("Found log file... " + row.Cells[0].Value.ToString());
                }
            }

            return files;
        }

        // Resizing methods
        private void Main_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == this.WindowState)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }

        // Resizing methods, return from tray on double click
        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void toolStripExit_Click(object sender, EventArgs e)
        {
            shutdownApp();
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("   Jax Uploader v" + Application.ProductVersion + "\n\n   http://limi.co.uk/\n\n   © Lloyd Pick 2011", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static string UploadFileEx(string uploadfile, string url, string fileFormName, string contenttype, NameValueCollection querystring, CookieContainer cookies)
        {
            if ((fileFormName == null) ||
                (fileFormName.Length == 0))
            {
                fileFormName = "file";
            }

            if ((contenttype == null) ||
                (contenttype.Length == 0))
            {
                contenttype = "application/octet-stream";
            }


            string postdata;
            postdata = "?";
            if (querystring != null)
            {
                foreach (string key in querystring.Keys)
                {
                    postdata += key + "=" + querystring.Get(key) + "&";
                }
            }
            Uri uri = new Uri(url + postdata);


            string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(uri);
            webrequest.CookieContainer = cookies;
            webrequest.ContentType = "multipart/form-data; boundary=" + boundary;
            webrequest.Method = "POST";


            // Build up the post message header

            StringBuilder sb = new StringBuilder();
            sb.Append("--");
            sb.Append(boundary);
            sb.Append("\r\n");
            sb.Append("Content-Disposition: form-data; name=\"");
            sb.Append(fileFormName);
            sb.Append("\"; filename=\"");
            sb.Append(Path.GetFileName(uploadfile));
            sb.Append("\"");
            sb.Append("\r\n");
            sb.Append("Content-Type: ");
            sb.Append(contenttype);
            sb.Append("\r\n");
            sb.Append("\r\n");

            string postHeader = sb.ToString();
            byte[] postHeaderBytes = Encoding.UTF8.GetBytes(postHeader);

            // Build the trailing boundary string as a byte array

            // ensuring the boundary appears on a line by itself

            byte[] boundaryBytes =
                   Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            FileStream fileStream = new FileStream(uploadfile,
                                        FileMode.Open, FileAccess.Read);
            long length = postHeaderBytes.Length + fileStream.Length +
                                                   boundaryBytes.Length;
            webrequest.ContentLength = length;

            Stream requestStream = webrequest.GetRequestStream();

            // Write out our post header

            requestStream.Write(postHeaderBytes, 0, postHeaderBytes.Length);

            // Write out the file contents

            byte[] buffer = new Byte[checked((uint)Math.Min(4096,
                                     (int)fileStream.Length))];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                requestStream.Write(buffer, 0, bytesRead);

            // Write out the trailing boundary

            requestStream.Write(boundaryBytes, 0, boundaryBytes.Length);
            WebResponse responce = webrequest.GetResponse();
            Stream s = responce.GetResponseStream();
            StreamReader sr = new StreamReader(s);

            return sr.ReadToEnd();
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            shutdownApp();
        }

        private void Main_FormClosing(object sender, EventArgs e)
        {
            shutdownApp();
        }

        private void shutdownApp()
        {
            foreach (DataGridViewRow row in this.logFilesDataView.Rows)
            {
                if (row.Cells[1].Value.ToString() == "True")
                {
                    logs.Add(row.Cells[0].Value.ToString());
                }
            }

            JaxUploader.Properties.Settings.Default.UploadedLogs = logs;
            JaxUploader.Properties.Settings.Default.Save();
            System.Windows.Forms.Application.Exit();
        }

    }
}
