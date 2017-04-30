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

        private ArrayList nSockets;
        private TcpListener tcpListener;
        private Socket handlerSocket;
        private string key = "123";
        private bool isAuth = false;
        [DllImport("user32")]
        public static extern void LockWorkStation();
        [DllImport("user32")]
        public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
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
            listBox1.Items.Add("File Written");
            File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "FILE_RECEIVED " + DateTime.Now + Environment.NewLine);
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
                        File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "COMMAND_AUTH - SUCCESS " + DateTime.Now + Environment.NewLine);
                    } else
                    {
                        this.sendString(stream, "FAIL");
                        File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "COMMAND_AUTH - FAIL " + DateTime.Now + Environment.NewLine);
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
                        File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "COMMAND_LIST " + DateTime.Now + Environment.NewLine);
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
                        File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "COMMAND_RECEIVE " + DateTime.Now + Environment.NewLine);
                    }
                    else if(dataRecieved.StartsWith("SHUT_DOWN"))
                    {
                        File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "SHUT_DOWN " + DateTime.Now + Environment.NewLine);
                        Process.Start("shutdown", "/s /t 10");
                    }
                    else if (dataRecieved.StartsWith("LOG_OFF"))
                    {
                        File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "LOG_OFF " + DateTime.Now + Environment.NewLine);
                        ExitWindowsEx(0, 0);
                    }
                    else if (dataRecieved.StartsWith("RESTART"))
                    {
                        File.AppendAllText("C:\\Users\\Khaled\\Documents\\Visual Studio 2015\\Projects\\FileSendServer\\FileSendServer\\Log.txt", "RESTART " + DateTime.Now + Environment.NewLine);
                        Process.Start("shutdown", "/r /t 10");
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
