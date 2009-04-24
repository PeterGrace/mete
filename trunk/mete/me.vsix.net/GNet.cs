using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;


namespace me.vsix.net
{
    [Serializable]
    public class GNet
    {
        // Globals
        System.Collections.Generic.Queue<byte[]> sendElevator;
        System.Collections.Generic.Queue<byte[]> recvElevator;
        [NonSerialized] 
        Socket tcpSocket;
        string sServer;
        int iPort;
        public delegate void raiseEvent(NotifyType msg, object obj);
        public event raiseEvent GenericCommEvent;
        byte[] buf;
        const int BUFSIZE = 1024;
        const int NOTIFY_CONNECTED = 1;
        const int NOTIFY_DISCONNECTED = 2;
        const int NOTIFY_RECEIVEDDATA = 3;
        const int NOTIFY_SOCKETEXCEPTION = -1;

        // Constructor
        public GNet(string server, int port)
        {
            buf = new byte[BUFSIZE];
            sServer = server;
            iPort = port;
        }

        // Connect, Close
        public void Connect()
        {
            sendElevator = new System.Collections.Generic.Queue<byte[]>();
            recvElevator = new System.Collections.Generic.Queue<byte[]>();

            tcpSocket = new Socket(AddressFamily.InterNetworkV6 , SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.BeginConnect(sServer, iPort, new AsyncCallback(cbConnect), tcpSocket);

        }
        public void Close()
        {
            tcpSocket.Close();
            tcpSocket = null;
            sendElevator.Clear();
            recvElevator.Clear();
            sendElevator = null;
            recvElevator = null;
            return;
        }

        public void sendBytes(byte[] input)
        {
            sendElevator.Enqueue(input);
            doSend();
            return;
        }
        public byte[] recvBytes()
        {
            try
            {
                return recvElevator.Dequeue();
            }
            catch (Exception ex)
            {
                GenericCommEvent(NotifyType.SocketException , ex);
                return null;
            }
        }

        private void doSend()
        {
            byte[] sendBytes;
            try
            {
                sendBytes = sendElevator.Dequeue();
                tcpSocket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, new AsyncCallback(cbSend), tcpSocket);
            }
            catch (Exception ex)
            {
                GenericCommEvent(NotifyType.SocketException, ex);
                return;
            }

        }

        // Callbacks
        private void cbConnect(IAsyncResult iar)
        {
            try
            {
                tcpSocket.EndConnect(iar);
                GenericCommEvent(NotifyType.Connected, null);
                IAsyncResult foo = tcpSocket.BeginReceive(buf, 0, BUFSIZE, SocketFlags.None, new AsyncCallback(cbReceive), tcpSocket);
            }
            catch (SocketException ex)
            {
                GenericCommEvent(NotifyType.SocketException, ex);
            }


        }

        private void cbSend(IAsyncResult iar)
        {
            

            Socket msock = (Socket)iar.AsyncState;
            msock.EndSend(iar);
        }

        private void cbReceive(IAsyncResult iar)
        {
            Socket msock = (Socket)iar.AsyncState;
            int recv = msock.EndReceive(iar);

            if (recv == 0)
                //GenericCommEvent(NotifyType.SocketException, null);
                return;

            byte[] freshByte = new byte[recv];
            Buffer.BlockCopy(buf, 0, freshByte, 0, recv);
            recvElevator.Enqueue(freshByte);
            GenericCommEvent(NotifyType.ReceivedData, null);
            try
            {
                tcpSocket.BeginReceive(buf, 0, BUFSIZE, SocketFlags.None, new AsyncCallback(cbReceive), tcpSocket);
            }
            catch (SocketException ex)
            {
                GenericCommEvent(NotifyType.SocketException, ex);
                tcpSocket.Close();
            }
        }
    }
    public enum NotifyType
    {
        Connected = 1,
        Disconnected = 2,
        ReceivedData = 3,
        SocketException = -1
    }

}
