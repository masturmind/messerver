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
        //client list in a form of a Hashtable
        public static Hashtable clientsList = new Hashtable();
        public static bool dying = false;
        public static List<Room> roomList = new List<Room>();
        //console keyboard command interface
        public static void Command()
        {
            string s;
            
            while (true)
            {


                s = Console.ReadLine();
                //determining the command
                if (s.Equals("stop")) { dying = true; Environment.Exit(0); }
                else if (s.Equals("yolo")) Console.WriteLine("yolo");
                else if (s.Equals("help"))
                {
                    Console.WriteLine("Available commands:");
                    Console.WriteLine("stop - Stops the server");
                    Console.WriteLine("create [room name] - Create a room");
                    Console.WriteLine("remove [room name] - Remove a room");
                    Console.WriteLine("list - View all rooms");
                }
                else if (s.Equals("list"))
                {
                    foreach (Room room in roomList)
                    { Console.WriteLine(room.ToString()); }
                }
                else if (s.StartsWith("create "))
                {
                    bool exists = false;
                    s = s.Remove(0, 7);
                    foreach (Room room in roomList)
                    { if (room.ToString() == s) exists = true; }
                    //can't create another room with the same name
                    if (exists)
                        Console.WriteLine("Room \"{0}\" exists", s);
                    else
                    {
                        Room room = new Room(s);
                        roomList.Add(room);
                        Console.WriteLine($"Room \"{s}\" created");
                    }

                }
                else if (s.StartsWith("remove "))
                {
                    bool exists = false;
                    Room roomToRemove = null;
                    s = s.Remove(0, 7);
                    foreach (Room room in roomList)
                    { if (room.ToString() == s) { exists = true; roomToRemove = room; } }

                    if (exists)
                    {
                        //at least one room is a necessary condition
                        if (roomList.Count > 1)
                        {
                            roomList.Remove(roomToRemove);
                            Console.WriteLine($"Room {s} removed");
                        }
                        else
                        {
                            Console.WriteLine($"At least one room needed");
                        }
                    }
                    else
                        Console.WriteLine($"Room {s} doesn't exist");
                }
                else
                {
                    Console.WriteLine("Better type \"help\"");
                }
                    if (dying) break;
            }
        }

        static void Main(string[] args)
        {
            TcpListener serverSocket = new TcpListener(8888);
            TcpClient clientSocket = default(TcpClient);
            serverSocket.Start();
            Console.WriteLine(" >> Server up");
            //a default room is created immediately upon running the app, for convenience
            Room defaultRoom = new Room("Default room");
            roomList.Add(defaultRoom);
            //a separate thread for the console commands
            Thread com = new Thread(Command);
            com.Start();
           
            while ((!dying))
            {
                clientSocket = serverSocket.AcceptTcpClient();

                byte[] bytesReceived = new byte[10025];
                string username = "";

                /*a character '$' is used as a terminator for every client-server message,
                also the message always starts with either "I:" or "M:", which specifies whether
                it's a text message from the client, or a background information for the app*/
                NetworkStream networkStream = clientSocket.GetStream();
                networkStream.Read(bytesReceived, 0, bytesReceived.Length);
                username = Encoding.Unicode.GetString(bytesReceived);
                username = username.Substring(0, username.IndexOf("$"));
                username = username.Substring(2, username.Length - 2);
                try
                {
                    clientsList.Add(username, clientSocket);
                    NetworkStream unameStream = clientSocket.GetStream();
                    byte[] unameBytes = Encoding.Unicode.GetBytes("I:UrOK$");
                    unameStream.Write(unameBytes, 0, unameBytes.Length);
                    unameStream.Flush();
                    //there's a new thread created for every client, handling them separately
                    handleClient client = new handleClient();
                    client.startClient(clientSocket, username, clientsList);

                }
                //handled case with an existing username, which triggers "can't add it to the hashtable" exception
                catch
                {
                    NetworkStream unameStream = clientSocket.GetStream();
                    byte[] unameBytes = Encoding.Unicode.GetBytes("I:UsernameExists$");
                    unameStream.Write(unameBytes, 0, unameBytes.Length);
                    unameStream.Flush();
                    unameStream.Dispose();
                    clientSocket.Close();
                    continue;
                }
                           
            }

            Console.WriteLine(" >> SHUTDOWN");
            Console.ReadLine();

           
        }

      
        public class Room
        {
            //a list of people inside the room
            public Hashtable insideList = new Hashtable();
            public string name;
            public Room(string name)
            {
                this.name = name;
            }
            public override string ToString()
            {
                return name;
            }

            public void AddUser(string username, TcpClient clientSocket)
            {
            
                    insideList.Add(username, clientSocket);

            }
            public void RemoveUser(string username)
            {
                    insideList.Remove(username);    
            }
            //a method for forwarding client messages to the entire room
            public void Broadcast(string message, string username, bool moderator, Hashtable insideList)
            {
                foreach (DictionaryEntry Item in insideList)
                {
                    TcpClient broadcastSocket = (TcpClient)Item.Value;
                    NetworkStream broadcastStream = broadcastSocket.GetStream();
                    byte[] broadcastBytes = null;

                    if (moderator == true)
                    {
                        broadcastBytes = Encoding.Unicode.GetBytes("M:" + message + "$");
                    }
                    else
                    {
                        broadcastBytes = Encoding.Unicode.GetBytes("M:" + username + " says: " + message + "$");
                    }

                    broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length);
                    broadcastStream.Flush();
                }
            }
        }
        //after username verification, this class enables to choose a room for the client and communication with him
        public class handleClient
        {
            TcpClient clientSocket;
            string username;
            Room room;
            public void startClient(TcpClient a, string b, Hashtable c)
            {
                this.clientSocket = a;
                this.username = b;
                Thread clientThread = new Thread(chat);
                clientThread.Start();
            }
            protected void chat()
            {
                bool roomGone = true;
                string outRooms = "";
                string chosenRoom = "";
                //server prepares and sends a list of all rooms separated by ":"
                foreach (Room room in roomList)
                {
                    outRooms += room.ToString() + ":";
                }
                NetworkStream roomStream = clientSocket.GetStream();
                byte[] roomBytes = Encoding.Unicode.GetBytes("I:" + outRooms + "$");
                roomStream.Write(roomBytes, 0, roomBytes.Length);
                roomStream.Flush();
                
                byte[] bytesReceived = new byte[10025];
                NetworkStream networkStream = clientSocket.GetStream();

                    networkStream.Read(bytesReceived, 0, bytesReceived.Length);

                chosenRoom = Encoding.Unicode.GetString(bytesReceived);
                chosenRoom = chosenRoom.Substring(0, chosenRoom.IndexOf("$"));
                chosenRoom = chosenRoom.Substring(2, chosenRoom.Length - 2);
                //handled the case when client got closed and room wasn't chosen
                if (chosenRoom != "NOPE")
                {
                    foreach (Room room in roomList)
                    {
                        if (room.ToString() == chosenRoom)
                        {
                            this.room = room;
                            room.AddUser(username, clientSocket);
                            Console.WriteLine(" >> " + username + " joined the room " + chosenRoom);
                            roomGone = false;
                            roomStream = clientSocket.GetStream();
                            roomBytes = Encoding.Unicode.GetBytes("I:UrOK$");
                            roomStream.Write(roomBytes, 0, roomBytes.Length);
                            roomStream.Flush();
                        }
                    }
                }
                //handled the case when client got closed and room wasn't chosen
                else
                {
                    clientsList.Remove(username);
                    Thread.CurrentThread.Abort();
                }
                //handled the case when the room chosen is already removed
                if (roomGone)
                {
                    roomStream = clientSocket.GetStream();
                    roomBytes = Encoding.Unicode.GetBytes("I:RoomGone$");
                    roomStream.Write(roomBytes, 0, roomBytes.Length);
                    roomStream.Flush();
                    clientsList.Remove(username);
                    Thread.CurrentThread.Abort();
                }
                //"welcoming" the new user to the room
                room.Broadcast("[INFO] " + username + " joined", username, true, room.insideList);
                bool stop = false;
                bytesReceived = new byte[10025];
                string dataFromClient = "";
                //while loop handling the communication with the client from now on
                while (!stop)
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
                        if (dataFromClient.StartsWith("I:"))
                        {
                            //ending the communication with a client, ending thread
                            dataFromClient = dataFromClient.Substring(2, dataFromClient.Length - 2);
                            if (dataFromClient.Equals("dying"))
                            {
                                networkStream.Dispose();
                                clientSocket.Close();
                                clientsList.Remove(username);
                                room.RemoveUser(username);
                                room.Broadcast("[INFO] " + username + " disconnected", username, true, room.insideList);
                                Console.WriteLine(" >> " + username + " disconnected");
                                stop = true;
                            }
                        }
                        if (dataFromClient.StartsWith("M:"))
                        {
                            dataFromClient = dataFromClient.Substring(2, dataFromClient.Length - 2);
                            //forwarding the message to the room
                            if (!dataFromClient.Equals(""))
                            {
                                Console.WriteLine(username + " says : " + dataFromClient);
                                room.Broadcast(dataFromClient, username, false, room.insideList);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                   
                        Console.WriteLine(ex.ToString());
                    }
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
