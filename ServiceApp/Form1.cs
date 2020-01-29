using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;

namespace ServiceApp
{
    #region Main
    public partial class Form1 : Form
    {
        TcpListener serverSocket = new TcpListener(1111);
        TcpClient clientSocket = default(TcpClient);
        TcpClient mainSocket = default(TcpClient);

        int counter = 0;
        static int m_portNum = 1111;

        public Form1()
        {
            InitializeComponent();
        }

        public void Start_Connection()
        {
            while (true)
            {
                counter++;
                clientSocket = serverSocket.AcceptTcpClient();
                NetworkStream networkStream = clientSocket.GetStream();
                networkStream.Write(Encoding.ASCII.GetBytes("Connected to Server."), 0, 20);

           //     networkStream.Flush();

                handleClinet client = new handleClinet();
                client.startClient(clientSocket, Convert.ToString(counter));
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                serverSocket.Start();
            }
            catch(SocketException ee)
            {
                return;
            }
            lbl_Status.Text = "Server Started";

            Thread acceptThread = new Thread(new ThreadStart(Start_Connection));
            acceptThread.Start();

            //    clientSocket.Close();
            //     serverSocket.Stop();

        }

    }
    #endregion

    #region Define Sending Data
    public struct Sending_Data
    {
        public int nType;   // 1 : Login    2 : Message     3 : Request_Screen      4 : Send_Screen         5 : Process  
        public int nLen;    //     nameLen      msgLen                                      size
        public byte[] bArr; //     Name         msg                                     ( byte ) Data

        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 * 300)]
    }
    #endregion
    #region Client Processing
    public class handleClinet
    {
        const int NONE = 0;
        const int LOGIN_WINDOWS_REQUEST = 1;
        const int LOGIN_MAC_REQUEST = 2;
        const int LOGIN_STATUS = 3;
        const int LOGIN_SUCCESS = 4;
        const int LOGIN_FAIL = 5;
        const int MESSAGE = 6;
        const int SCREEN_REQUEST = 7;
        const int SCREEN_DATA = 8;

        bool IS_MAIN = false;

        TcpClient m_Socket = new TcpClient();
        static TcpClient mainSocket = new TcpClient();
        static TcpClient macSocket = new TcpClient();

        Sending_Data m_sendingData;
        string clNo;
        public void startClient(TcpClient inClientSocket, string clineNo)
        {
            this.m_Socket = inClientSocket;
            this.clNo = clineNo;
            Thread ctThread = new Thread(doChat);
            ctThread.Start();
        }
        public byte[] getBytes(Sending_Data data)
        {
            List<byte> list = new List<byte>(data.bArr.Length + 8);

            list.AddRange(BitConverter.GetBytes(data.nType));
            list.AddRange(BitConverter.GetBytes(data.nLen));
            list.AddRange(data.bArr);
            
            return list.ToArray();
        }
        public Sending_Data fromBytes(byte[] arr)
        {
            Sending_Data str = new Sending_Data();
            byte[] bytenum = new byte[4];

            for (int i = 0; i < 4; i++)
                bytenum[i] = arr[i];
            str.nType = BitConverter.ToInt32(bytenum, 0);

            for (int i = 0; i < 4; i++)
                bytenum[i] = arr[i + 4];
            str.nLen = BitConverter.ToInt32(bytenum, 0);

            if (arr.Length < 8) str.nLen = 0;

            bytenum = new byte[str.nLen];
            for (int i = 0; i < str.nLen; i++)
                bytenum[i] = arr[i + 8];

            str.bArr = bytenum;
            return str;
        }
        public delegate void DelegateShowScreenImage(int size, byte[] arr);
        public void ShowScreenImage(int size, byte[] arr)
        {
//              if (pictureBox1.InvokeRequired)
//              {
//                  var d = new DelegateShowScreenImage(ShowScreenImage);
//                  pictureBox1.Invoke(d, new object[] { size, arr });
//              }
//              else
//              {
//                  pictureBox1.Image = Image.FromStream(new MemoryStream(arr));
//              }
        }
        public int getLength(Sending_Data m_sendingData)
        {
            return 8 + m_sendingData.nLen;
        }
        public void RequestLoginToMain(Sending_Data m_sendingData, NetworkStream netowrkStream)
        {

            netowrkStream.Write(getBytes(m_sendingData), 0, getLength(m_sendingData));
        }
        public void SendLoginSutatusToClient(Sending_Data m_sendingData, NetworkStream netowrkStream)
        {

            netowrkStream.Write(getBytes(m_sendingData), 0, getLength(m_sendingData));
        }
        public void RequestScreenToClient(Sending_Data m_sendingData, NetworkStream netowrkStream)
        {

            netowrkStream.Write(getBytes(m_sendingData), 0, getLength(m_sendingData));
        }
        public void SendScreenToMain(Sending_Data m_sendingData, NetworkStream netowrkStream)
        {

            netowrkStream.Write(getBytes(m_sendingData), 0, getLength(m_sendingData));
        }
        private void doChat()
        {
            

            byte[] data = new byte[1024 * 1000];
            while (true)
            {
                try
                {
                    NetworkStream networkStream = m_Socket.GetStream();
                    int len = networkStream.Read(data, 0, m_Socket.ReceiveBufferSize);

                    if (len <= 0) continue;
                    m_sendingData = fromBytes(data);

                    if (m_sendingData.nType == LOGIN_WINDOWS_REQUEST) // MainClient
                    {
                        mainSocket = m_Socket;
            //            RequestLoginToMain(m_sendingData, macSocket.GetStream());
                    }
                    else if (m_sendingData.nType == LOGIN_MAC_REQUEST) // MacClient
                    {
                        macSocket = m_Socket;
                        RequestLoginToMain(m_sendingData, mainSocket.GetStream());
                    }
                    else if (m_sendingData.nType == LOGIN_STATUS)
                    {
                        mainSocket = m_Socket;
                        SendLoginSutatusToClient(m_sendingData, macSocket.GetStream());
                    }
                    else if (m_sendingData.nType == SCREEN_REQUEST)
                    {
                        mainSocket = m_Socket;
                        RequestScreenToClient(m_sendingData, macSocket.GetStream());
                    }
                    else if (m_sendingData.nType == SCREEN_DATA)
                    {
                        macSocket = m_Socket;
                        SendScreenToMain(m_sendingData, mainSocket.GetStream());
                        ShowScreenImage(m_sendingData.nLen, m_sendingData.bArr);
                    }
               //     networkStream.Flush();
                }
                catch (Exception ee)
                {
                    //    lbl_Status.Text = ee.Message;
                    return;
                }

            }
        }
    }
    #endregion
}