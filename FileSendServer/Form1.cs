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

namespace FileSendServer
{
    public partial class Form1 : Form
    {

        private ArrayList nSockets;
        private TcpListener tcpListener;
        private Socket handlerSocket;
        private string key = "123";
        private bool isAuth = false;
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
                Stream fileStream = File.OpenWrite("C:\\Users\\user\\Documents\\SubmittedFsssile.txt");
                while (true)
                {
                    thisRead = networkStream.Read(dataByte, 0, blockSize);
                    fileStream.Write(dataByte, 0, thisRead);
                    if (thisRead == 0) break;
                }
                fileStream.Close();
            }
            listBox1.Items.Add("File Written");
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
            TcpClient client = tcpListener.AcceptTcpClient();
            NetworkStream stream = client.GetStream();
            byte[] buffer; int bytesRead; string dataRecieved;
            while (true)
            {
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
                    } else
                    {
                        this.sendString(stream, "FAIL");
                    }
                }
                else if (isAuth)
                {
                    if (dataRecieved.StartsWith("COMMAND_LIST"))
                    {
                        DirectoryInfo d = new DirectoryInfo(@"C:\Users\user\Documents");//Assuming Test is your Folder
                        FileInfo[] Files = d.GetFiles(); //Getting Text files
                        string str = "";
                        foreach (FileInfo file in Files)
                        {
                            str = str + ", " + file.Name;
                        }
                        this.sendString(stream, str);
                    }
                    else if(dataRecieved.StartsWith("COMMAND_RECIEVE"))
                    {
                        string fileName = dataRecieved.Remove(0, "COMMAND_RECIEVE:".Length);
                        if (File.Exists(@"C:\Users\user\Documents\" + fileName.Trim()))
                        {
                            byte[] fileBytes = File.ReadAllBytes(@"C:\Users\user\Documents\" + fileName.Trim());
                            stream.Write(fileBytes, 0, fileBytes.Length);
                            stream.Flush();
                        }
                    }
                }
                else
                {
                    this.sendString(stream, "UNAUTHORIZED");
                }

            }
        }

      
    }
}
