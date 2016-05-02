using System;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace GadgeteerApp3
{
    static class MySocketFunctions
    {

        public static string socketReadLine(Socket handler)
        {
            string data = null;
            byte[] bytes = new Byte[1];
            while (true)
            {
                bytes = new byte[1];
                int bytesRec = -1;
                bytesRec = handler.Receive(bytes);
                char[] chars = Encoding.UTF8.GetChars(bytes, 0, bytesRec);
                string temp =new string(chars, 0, chars.Length);
                if (temp.Equals("\n"))
                    break;
                else
                    data += temp;
            }
            return data;

        }
        public static int socketWriteLine(Socket sender, string message)
        {
            byte[] msg = Encoding.UTF8.GetBytes(message + "\n");

            // Send the data through the socket.
            int bytesSent = sender.Send(msg);
            return bytesSent;

        }
        public static void socketSendFile(Socket handler, byte[] tosend)
        {
            
            int effective_sent = 0;
            int total_byte = (int)tosend.Length;

            if (total_byte >= 0)
            {
            
                try
                {
                    socketWriteLine(handler, total_byte.ToString());
                    while (total_byte != 0)
                    {

                        effective_sent = handler.Send(tosend);
                        total_byte -= effective_sent;


                    }                   
                }
                catch (SocketException)
                {
                   
                }
            }


        }

    }
}
