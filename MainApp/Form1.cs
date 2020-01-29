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
    #region Define Sending Data
    public struct Sending_Data
    {
        public int nType;   //      Login       Message      Request_Screen      Send_Screen         Process  
        public int nLen;    //     nameLen      msgLen                            size
        public byte[] bArr; //     Name         msg                              ( byte ) Data

        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 * 300)]
    }
    #endregion
    #region Main
    public partial class Form1 : Form
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

        int m_nSelectedUser = -1;

        string m_IpAddress = "127.0.0.1";
        int m_portNum = 1111;

        public Sending_Data m_sendingData = new Sending_Data();

        TcpClient clientSocket = new TcpClient();
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
        public Form1()
        {
            InitializeComponent();
        }

        public delegate void DelegateLoginNewUser(Sending_Data m_sendingData, NetworkStream networkStream);
        public delegate void DelegateShowScreenImage(int size, byte[] arr);
        public delegate void DelegateShowStatus(string msg);
        public void LoginNewUser(Sending_Data m_sendingData, NetworkStream networkStream)
        {
            if (listBox1.InvokeRequired)
            {
                var d = new DelegateLoginNewUser(LoginNewUser);
                listBox1.Invoke(d, new object[] { m_sendingData, networkStream });
            }
            else
            {
                listBox1.Items.Add(Encoding.ASCII.GetString(m_sendingData.bArr));
                m_sendingData.nType = LOGIN_STATUS;
                m_sendingData.nLen = LOGIN_SUCCESS;
                m_sendingData.bArr = new byte[] { 1,2,3,4 };
                networkStream.Write(getBytes(m_sendingData), 0, 8 + m_sendingData.nLen); // Login successed 
            }
        }
        public void ShowScreenImage(int size, byte[] arr)
        {
            if (pictureBox1.InvokeRequired)
            {
                var d = new DelegateShowScreenImage(ShowScreenImage);
                pictureBox1.Invoke(d, new object[] { size, arr });
            }
            else
            {
                pictureBox1.Image = Image.FromStream(new MemoryStream(arr));
            }
        }
        public void ShowStatus(string msg)
        {
            if (lbl_Status.InvokeRequired)
            {
                var d = new DelegateShowStatus(ShowStatus);
                lbl_Status.Invoke(d, new object[] { msg });
            }
            else
            {
                lbl_Status.Text = msg;
            }
        }
        public void DoWork()    // Receiving Operation
        {
            byte[] data = new byte[1024 * 1000];
            while (true)
            {
                try
                {
                    NetworkStream networkStream = clientSocket.GetStream();
                    int len = networkStream.Read(data, 0, clientSocket.ReceiveBufferSize);

                    if (len <= 0) continue;
                    m_sendingData = fromBytes(data);

                    if (m_sendingData.nType == LOGIN_MAC_REQUEST)
                        LoginNewUser(m_sendingData, networkStream);
                    else if (m_sendingData.nType == SCREEN_DATA)
                        ShowScreenImage(m_sendingData.nLen, m_sendingData.bArr);

                //    networkStream.Flush();
                }
                catch (Exception ee)
                {
                    ShowStatus( ee.Message);
                    return;
                }

            }
        }        
        private void Form1_Load(object sender, EventArgs e)
        {
            

        }
        private void btn_Connect_Click(object sender, EventArgs e)
        {
            if (txt_ServerIP.Text == "" || txt_PortNum.Text == "")
                return;
            try
            {
                m_IpAddress = txt_ServerIP.Text;
                m_portNum = int.Parse(txt_PortNum.Text);

                clientSocket.Connect(txt_ServerIP.Text, int.Parse(txt_PortNum.Text));
           //     Thread.Sleep(10);
                NetworkStream serverStream = clientSocket.GetStream();
                byte[] inStream = new byte[clientSocket.ReceiveBufferSize];
                serverStream.Read(inStream, 0, clientSocket.ReceiveBufferSize);
                lbl_Status.Text = Encoding.ASCII.GetString(inStream);

                // Main request Login to Server
                m_sendingData.nType = LOGIN_WINDOWS_REQUEST;
                m_sendingData.nLen = 4;
                m_sendingData.bArr = Encoding.ASCII.GetBytes("Main");
                serverStream.Write(getBytes(m_sendingData), 0, 8 + 4);

                Thread thread = new Thread(new ThreadStart(DoWork));
                thread.IsBackground = true;
                thread.Start();

            //    serverStream.Flush();

            }
            catch(SocketException ee)
            {
                lbl_Status.Text = ee.Message;
            }
        }
        public void RequestForScreen()
        {
            
            try
            {
                m_sendingData.nType = SCREEN_REQUEST;
                m_sendingData.bArr = Encoding.ASCII.GetBytes("SCREEN");
                m_sendingData.nLen = 6;
                clientSocket.Close();
                clientSocket = new TcpClient(m_IpAddress, m_portNum);
                clientSocket.Connect(m_IpAddress, m_portNum);

                NetworkStream serverStream = clientSocket.GetStream();

                serverStream.Write(getBytes(m_sendingData), 0, 8 + m_sendingData.nLen);
            }
            catch(SocketException ee)
            {
                lbl_Status.Text = ee.Message;
                return;
            }
        }
        private void showScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_nSelectedUser = listBox1.SelectedIndex;
            if (m_nSelectedUser < 0 || m_nSelectedUser > listBox1.Items.Count)
                return;
            RequestForScreen();
        }
        private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            contextMenuStrip1.Hide();
            m_nSelectedUser = listBox1.SelectedIndex;
        }

    }
    #endregion
}