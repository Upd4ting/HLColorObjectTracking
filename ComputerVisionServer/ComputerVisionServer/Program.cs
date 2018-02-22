using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

using LiteNetLib;
using LiteNetLib.Utils;

namespace ComputerVisionServer {
    public class Program {
        private static void Main(string[] args) {
            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager            server   = new NetManager(listener, 64, "ConnectionKey");
            server.MergeEnabled = true;
            server.Start(32020);

            // PROTOCOL
            // INT -> Number of request
            // 	INT -> minH
            // 	INT -> maxH
            //  ... 
            // INT -> Size of the picture
            // BYTES -> IMAGE
            listener.NetworkReceiveEvent += (peer, reader) => {
                List<Request> requests      = new List<Request>();
                int           numberRequest = reader.GetInt();

                for (int i = 0; i < numberRequest; i++) {
                    Request r = new Request();
                    r.minH = reader.GetInt();
                    r.maxH = reader.GetInt();
                    r.posX = -1;
                    r.posY = -1;
                    requests.Add(r);
                }

                int size = reader.GetInt();

                byte[] pictures = new byte[size];

                reader.GetBytes(pictures, size);

                int n = size / 2 - 1;

                for (int i = 0; i < size; i+=4) {
                    byte b = pictures[i];
                    byte g = pictures[i + 1];
                    byte r = pictures[i + 2];

                    Color color = Color.FromArgb(r, g, b);

                    float h = color.GetHue();

                    for (int j = 0; j < requests.Count; j++) {
                        Request request = requests.ElementAt(j);
                        if (h >= request.minH && h <= request.maxH) {
                            request.posY = i / n;
                            request.posX = i % n;
                        }
                    }
                }

                // PROTOCOL ANSWER
                // Num request
                // INT POSX
                // INT POSY
                // For each request

                NetDataWriter writer = new NetDataWriter();

                writer.Put(numberRequest);

                foreach (Request request in requests) {
                    writer.Put(request.posX);
                    writer.Put(request.posY);
                }

                peer.Send(writer, SendOptions.ReliableOrdered);
            };

            while (!Console.KeyAvailable) {
                server.PollEvents();
                Thread.Sleep(15);
            }

            server.Stop();
        }
    }
}