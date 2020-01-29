
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
using System.Runtime.InteropServices;

namespace ClientApp
{
    public struct Sending_Data
    {
        public int nType;   // 1 : Login    2 : Message     3 : Request_Screen      4 : Send_Screen         5 : Process  
        public int nLen;    //     nameLen      msgLen                                      size
        public byte[] bArr; //     Name         msg                                     ( byte ) Data

        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024 * 300)]
    }
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
        bool IS_LOGIN = false;
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

        public delegate void DelegateLoginStatus(Sending_Data m_sendingData);
        public delegate void DelegateShowStatus(string msg);
        public void LoginStatus(Sending_Data m_sendingData)
        {
            if (lbl_Status.InvokeRequired)
            {
                var d = new DelegateLoginStatus(LoginStatus);
                lbl_Status.Invoke(d, new object[] { m_sendingData });
            }
            else
            {
                if (m_sendingData.nLen == LOGIN_SUCCESS) // success
                {
                    IS_LOGIN = true;
                    lbl_Status.Text = "Login Successed !";
                }
                else
                {
                    IS_LOGIN = false;
                    lbl_Status.Text = "Login Failed !";
                }
            }
        }
        public void SendScreenImage(Sending_Data m_sendingData, NetworkStream networkStream)
        {
            while (IS_LOGIN)
            {
                Bitmap bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                Graphics g = Graphics.FromImage(bitmap);
                g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);

                MemoryStream ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);

                m_sendingData.nType = SCREEN_DATA;
                m_sendingData.nLen = (int)ms.Length;
                m_sendingData.bArr = ms.ToArray();

                networkStream.Write(getBytes(m_sendingData), 0, (int)ms.Length + 8);


                Thread.Sleep(100);
           //     ms.Flush();
           //     networkStream.Flush();
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

                    if (m_sendingData.nType == LOGIN_STATUS)
                        LoginStatus(m_sendingData);
                    else if (m_sendingData.nType == SCREEN_REQUEST)
                        SendScreenImage(m_sendingData, networkStream);

                //    networkStream.Flush();
                }
                catch (Exception ee)
                {
                    ShowStatus(ee.Message);
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
                clientSocket.Connect(txt_ServerIP.Text, int.Parse(txt_PortNum.Text));
                Thread.Sleep(10);
                NetworkStream serverStream = clientSocket.GetStream();
                byte[] inStream = new byte[clientSocket.ReceiveBufferSize];
                serverStream.Read(inStream, 0, clientSocket.ReceiveBufferSize);
                lbl_Status.Text = Encoding.ASCII.GetString(inStream);

                // Mac Request Login to Server
                m_sendingData.nType = LOGIN_MAC_REQUEST; // LOGIN_REQUEST : MacClient
                m_sendingData.nLen = txt_UserName.Text.Length;
                m_sendingData.bArr = Encoding.ASCII.GetBytes(txt_UserName.Text);

                serverStream.Write(getBytes(m_sendingData), 0, txt_UserName.Text.Length + 8);

                Thread thread = new Thread(new ThreadStart(DoWork));
                thread.IsBackground = true;
                thread.Start();
          //      serverStream.Flush();
            }
            catch (SocketException ee)
            {
                lbl_Status.Text = ee.Message;
            }
        }
    }
}