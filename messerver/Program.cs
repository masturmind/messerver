using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.IO;
using System.Diagnostics;

namespace messerver
{
    class Program
    {
        public static Hashtable clientsList = new Hashtable();
        public static bool dying = false;
        public static List<string> roomList = new List<string>();
        public static void Command()
        {
            string s;
            
            while (true)
            {


                s = Console.ReadLine();
                if (s.Equals("stop")) { dying = true; Environment.Exit(0); }
                if (s.Equals("yolo")) Console.WriteLine("yolo");
                if (s.StartsWith("create "))
                {
                    s = s.Remove(0, 7);
                    Console.WriteLine($"Room {s} created");
                } ////
                    if (dying) break;
            }
        }

        static void Main(string[] args)
        {
            
            TcpListener serverSocket = new TcpListener(8888);
            TcpClient clientSocket = default(TcpClient);
            serverSocket.Start();
            Console.WriteLine(" >> Server up");
            Thread com = new Thread(Command);
            com.Start();
           
            while ((!dying))
            {
                clientSocket = serverSocket.AcceptTcpClient();

               // Thread.Sleep(500);
                byte[] bytesReceived = new byte[10025];
                string username = "";
                

                NetworkStream networkStream = clientSocket.GetStream();
                networkStream.Read(bytesReceived, 0, bytesReceived.Length);
                username = Encoding.Unicode.GetString(bytesReceived);
                username = username.Substring(0, username.IndexOf("$"));
                username = username.Substring(2, username.Length - 2);
                try
                {
                    clientsList.Add(username, clientSocket);
                    NetworkStream errorStream = clientSocket.GetStream();
                    byte[] errorBytes = Encoding.Unicode.GetBytes("I:UrOK$");
                    errorStream.Write(errorBytes, 0, errorBytes.Length);
                    errorStream.Flush();
                   
                }
                catch
                {
                    NetworkStream errorStream = clientSocket.GetStream();
                    byte[] errorBytes = Encoding.Unicode.GetBytes("I:UsernameExists$");
                    errorStream.Write(errorBytes, 0, errorBytes.Length);
                    errorStream.Flush();
                    errorStream.Dispose();
                    clientSocket.Close();
                    continue;
                }

                    broadcast("[INFO] " + username + " joined", username, true);
                    Console.WriteLine(" >> " + username + " joined the chat room"); //rooms
                    handleClient client = new handleClient();
                    client.startClient(clientSocket, username, clientsList);              
            }

            Console.WriteLine(" >> SHUTDOWN");
            Console.ReadLine();

           
        }

        public static void broadcast(string message, string username, bool moderator)
        {
            foreach (DictionaryEntry Item in clientsList)
            {
                TcpClient broadcastSocket = (TcpClient)Item.Value;
                NetworkStream broadcastStream = broadcastSocket.GetStream();
                byte[] broadcastBytes = null;

                if (moderator == true)
                {
                    broadcastBytes = Encoding.Unicode.GetBytes("M:"+ message+ "$");
                }
                else
                {
                    broadcastBytes = Encoding.Unicode.GetBytes("M:" + username + " says: " + message+"$");
                }

                broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                broadcastStream.Flush();


            }
            
        }
        public class handleClient
        {
            TcpClient clientSocket;
            string username;
            Hashtable clientsList;

            public void startClient(TcpClient a, string b, Hashtable c)
            {
                this.clientSocket = a;
                this.username = b;
                this.clientsList = c;
                Thread clientThread = new Thread(chat);
                clientThread.Start();
            }
            protected void chat()
            {
                bool stop = false;
                byte[] bytesReceived = new byte[10025];
                string dataFromClient = "";
                NetworkStream networkStream = default(NetworkStream);
                while ((true))
                {
                    if (dying)
                    {
                        sendInfo(clientSocket,"shutdown");
                        networkStream.Dispose();
                        clientSocket.Close();
                        break;
                    }


                    try
                    {
                        networkStream = clientSocket.GetStream();
                        networkStream.Read(bytesReceived, 0, bytesReceived.Length);
                        dataFromClient = Encoding.Unicode.GetString(bytesReceived);
                        dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
                        //dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
                        if (dataFromClient.StartsWith("I:"))
                        {
                            dataFromClient = dataFromClient.Substring(2, dataFromClient.Length - 2);
                            if (dataFromClient.Equals("dying"))
                            {
                                networkStream.Dispose();
                                clientSocket.Close();
                                clientsList.Remove(username);
                                broadcast("[INFO] " + username + " disconnected", username, true);
                                Console.WriteLine(" >> " + username + " disconnected");
                                stop = true;
                            }
                        }
                        if (dataFromClient.StartsWith("M:"))
                        {
                            dataFromClient = dataFromClient.Substring(2, dataFromClient.Length - 2);
                            if (!dataFromClient.Equals(""))
                            {
                                Console.WriteLine(username + " says : " + dataFromClient);
                                Program.broadcast(dataFromClient, username, false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine(ex.ToString());
                    }
                    if (stop) break;
                }
            }

            private void sendInfo(TcpClient clientSocket, string info)
            {
                NetworkStream serverStream = default(NetworkStream);
                serverStream = clientSocket.GetStream();
                byte[] outStream = Encoding.Unicode.GetBytes("I:" + info + "$");
                serverStream.Write(outStream, 0, outStream.Length);

            }
        }


    }
        


    }
