using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FileSendServer
{
    public partial class Form1 : Form
    {
        [DllImport("kernel32.dll")]
        private extern static void GetSystemTime(ref SYSTEMTIME lpSystemTime);
        private ArrayList nSockets;
        private TcpListener tcpListener;
        private Socket handlerSocket;
        private string key = "123";
        private bool isAuth = false;
        [DllImport("user32")]
        public static extern void LockWorkStation();
        [DllImport("user32")]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        public static String logFileDir = "C:\\Users\\Khaled\\networks2\\Log.txt";

        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            IPHostEntry IPHost = Dns.GetHostByName(Dns.GetHostName());
            label1.Text = "My IP address is " + IPHost.AddressList[0].ToString();
            tcpListener = new TcpListener(8080);
            Thread t = new Thread(new ThreadStart(commandHandler));
            t.Start();
        }

        public void listenerThread()
        {
            tcpListener = new TcpListener(8080);
            tcpListener.Start();
            while (true)
            {
                handlerSocket = tcpListener.AcceptSocket();
                if (handlerSocket.Connected)
                {
                    Control.CheckForIllegalCrossThreadCalls = false;
                    listBox1.Items.Add(
                    handlerSocket.RemoteEndPoint.ToString() + " connected.");
                    lock (this)
                    {
                        nSockets.Add(handlerSocket);
                    }
                    ThreadStart thdstHandler = new ThreadStart(handlerThread);
                    Thread thdHandler = new Thread(thdstHandler);
                    thdHandler.Start();
                }
            }
        }

        public void handlerThread()
        {
            Socket handlerSocket = (Socket)nSockets[nSockets.Count - 1];
            NetworkStream networkStream = new NetworkStream(handlerSocket);
            int thisRead = 0;
            int blockSize = 1024;
            Byte[] dataByte = new Byte[blockSize];
            lock (this)
            {
                // Only one process can access
                // the same file at any given time
                Stream fileStream = File.OpenWrite("C:\\Users\\Khaled\\Documents\\SubmittedFsssile.txt");
                while (true)
                {
                    thisRead = networkStream.Read(dataByte, 0, blockSize);
                    fileStream.Write(dataByte, 0, thisRead);
                    if (thisRead == 0) break;
                }
                fileStream.Close();
            }
            listBox1.BeginInvoke((Action)(() => listBox1.Items.Add("File Written")));
            File.AppendAllText(logFileDir, "FILE_RECEIVED " + DateTime.Now + Environment.NewLine);
            //handlerSocket = null;
        }
        public void sendString(NetworkStream ns, string str)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            ns.Write(buffer, 0, buffer.Length);
            ns.Flush();
        }
        public void commandHandler()
        {
            tcpListener.Start();
            byte[] buffer; int bytesRead; string dataRecieved; TcpClient client; NetworkStream stream;
            while (true)
            {
                 client = tcpListener.AcceptTcpClient();
                 stream = client.GetStream();
                buffer = new byte[client.ReceiveBufferSize];
                bytesRead = stream.Read(buffer, 0, client.ReceiveBufferSize);
                dataRecieved = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                listBox1.BeginInvoke((Action)(() => listBox1.Items.Add(dataRecieved)));
                if (dataRecieved.StartsWith("COMMAND_AUTH"))
                {
                    string sentKey = dataRecieved.Remove(0, "COMMAND_AUTH:".Length);
                    if (sentKey == key) {
                        isAuth = true;
                        this.sendString(stream, "SUCCESS");
                        File.AppendAllText(logFileDir, "COMMAND_AUTH - SUCCESS " + DateTime.Now + Environment.NewLine);
                    } else
                    {
                        this.sendString(stream, "FAIL");
                        File.AppendAllText(logFileDir, "COMMAND_AUTH - FAIL " + DateTime.Now + Environment.NewLine);
                    }
                    stream.Close();
                }
                else if (isAuth)
                {
                    if (dataRecieved.StartsWith("COMMAND_LIST"))
                    {
                        DirectoryInfo d = new DirectoryInfo(@"C:\Users\Khaled\Documents");//Assuming Test is your Folder
                        FileInfo[] Files = d.GetFiles(); //Getting Text files
                        string str = "";
                        foreach (FileInfo file in Files)
                        {
                            str = str + ", " + file.Name;
                        }
                        this.sendString(stream, str);
                        stream.Close();
                        File.AppendAllText(logFileDir, "COMMAND_LIST " + DateTime.Now + Environment.NewLine);
                    }
                    else if(dataRecieved.StartsWith("COMMAND_RECIEVE"))
                    {
                        string fileName = dataRecieved.Remove(0, "COMMAND_RECIEVE:".Length);
                        if (File.Exists(@"C:\Users\Khaled\Documents\" + fileName.Trim()))
                        {
                            Stream fileStream = File.OpenRead(@"C:\Users\Khaled\Documents\" + fileName.Trim());
                            byte[] fileBuffer = new byte[fileStream.Length];
                            fileStream.Read(fileBuffer, 0, (int)fileStream.Length);
                            // Open a TCP/IP Connection and send the data
                            stream.Write(fileBuffer, 0, fileBuffer.GetLength(0));
                            stream.Close();
                        }
                        File.AppendAllText(logFileDir, "COMMAND_RECEIVE " + fileName +"  " + DateTime.Now + Environment.NewLine);
                    }
                    else if(dataRecieved.StartsWith("TIME_CHANGE"))
                    {
                        SYSTEMTIME stime = new SYSTEMTIME();
                        GetSystemTime(ref stime);
                        this.sendString(
                            stream, "/C time " + stime.wHour.ToString() + ":" + stime.wMinute.ToString() + ":" + stime.wSecond.ToString()+" PM");
                            
                        stream.Close();
                    }
                    else if(dataRecieved.StartsWith("SHUT_DOWN"))
                    {
                        File.AppendAllText(logFileDir, "SHUT_DOWN " + DateTime.Now + Environment.NewLine);
                        Process.Start("shutdown", "/s /t 10");
                    }
                    else if (dataRecieved.StartsWith("LOG_OFF"))
                    {
                        File.AppendAllText(logFileDir, "LOG_OFF " + DateTime.Now + Environment.NewLine);
                        ExitWindowsEx(0, 0);
                    }
                    else if (dataRecieved.StartsWith("RESTART"))
                    {
                        File.AppendAllText(logFileDir, "RESTART " + DateTime.Now + Environment.NewLine);
                        Process.Start("shutdown", "/r /t 10");
                    }
                    else if (dataRecieved.StartsWith("COMMAND_DELETE"))
                    {

                        string fileNameD = dataRecieved.Remove(0, "COMMAND_DELETE".Length);
                        if (File.Exists(@"C:\Users\Khaled\Documents\" + fileNameD.Trim()))
                        {
                            File.Delete(@"C:\Users\Khaled\Documents\" + fileNameD.Trim());
                            this.sendString(stream, "SUCCESS");
                        } else
                        {
                            this.sendString(stream, "FAIL");
                        }
                        File.AppendAllText(logFileDir, "Delete "+ fileNameD + "  "+ DateTime.Now + Environment.NewLine);
                    }
                    else if (dataRecieved.StartsWith("COMMAND_SEND"))
                    {
                        //Socket handlerSocket = (Socket)nSockets[nSockets.Count - 1];
                        string fileNameS = dataRecieved.Remove(0, "COMMAND_SEND:".Length);
                        this.sendString(stream, "COMMAND_SEND_ACKNOWLEGED");
                        int thisRead = 0;
                        int blockSize = 1024;
                        Byte[] dataByte = new Byte[blockSize];
                        lock (this)
                        {
                            // Only one process can access
                            // the same file at any given time
                            Stream fileStream1 = File.OpenWrite((@"C:\Users\Khaled\networks2\" + fileNameS));
                            while (true)
                            {
                                thisRead = stream.Read(dataByte, 0, blockSize);
                                fileStream1.Write(dataByte, 0, thisRead);
                                if (thisRead == 0) break;
                            }
                            fileStream1.Close();
                        }
                        listBox1.BeginInvoke((Action)(() => listBox1.Items.Add("File Written")));

                        File.AppendAllText(logFileDir, "FILE_SENT "+ fileNameS + DateTime.Now + Environment.NewLine);
                    }
                }
                else
                {
                    this.sendString(stream, "UNAUTHORIZED");
                    stream.Close();
                }

            }
        }

      
    }
}
