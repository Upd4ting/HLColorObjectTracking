#include <iostream>
#include <signal.h>
#include <algorithm>
#include <cstdlib>
#include <cmath>
#include <sstream>
#include "ServerUDP.h"
#include "PropFile.h"
#include "Input.h"

using namespace std;

typedef unsigned char u8;

typedef struct Request {
	int minH;
	int maxH;
	int posX;
	int posY;
} REQUEST;

ServerUDP *server = NULL;

Logger *logger = NULL;

void RGBtoHSV(float fR, float fG, float fB, float& fH, float& fS, float& fV) {
  float fCMax = max(max(fR, fG), fB);
  float fCMin = min(min(fR, fG), fB);
  float fDelta = fCMax - fCMin;
  
  if(fDelta > 0) {
    if(fCMax == fR) {
      fH = 60 * (fmod(((fG - fB) / fDelta), 6));
    } else if(fCMax == fG) {
      fH = 60 * (((fB - fR) / fDelta) + 2);
    } else if(fCMax == fB) {
      fH = 60 * (((fR - fG) / fDelta) + 4);
    }
    
    if(fCMax > 0) {
      fS = fDelta / fCMax;
    } else {
      fS = 0;
    }
    
    fV = fCMax;
  } else {
    fH = 0;
    fS = 0;
    fV = fCMax;
  }
  
  if(fH < 0) {
    fH = 360 + fH;
  }
}

int getInt(char *buffer, int start)
{
	unsigned int num = ((u8)buffer[start]) | ((u8)buffer[start + 1] << 8) | ((u8)buffer[start + 2] << 16) | ((u8)buffer[start + 3] << 24);
	return num;
}

void writeInt(char *buffer, int start, int n)
{
	buffer[start] = n & 0xff;
	buffer[start + 1] = (n>>8)  & 0xff;
	buffer[start + 2] = (n>>16) & 0xff;
	buffer[start + 3] = (n>>24) & 0xff;
}

void handlerSigint(int sig)
{
	logger->write("Closing the server...");

	if (server)
		server->stop();
}

int main(int argc, char **argv)
{
	// Armement d'un signal
	struct sigaction act;
	act.sa_handler = handlerSigint;
	sigemptyset(&act.sa_mask);
	act.sa_flags = 0;
	sigaction(SIGINT, &act, 0);
	// Fin armement

	PropFile prop("server.conf");
	logger = new Logger("server.log");

	server = ServerUDP::create(prop["ip"], convertInt(prop["port"]), convertInt(prop["threads"]), logger);

	if (server == NULL)
	{
		logger->write("Error when creating the server!");
		return 1;
	}

	// PROTOCOL
	// INT -> Number of request
	// 	INT -> minH
	// 	INT -> maxH
	//  ... 
	// INT -> Size of the picture
	// BYTES -> IMAGE

	server->getSocket()->setMessageListener([&](Socket *ss) {
		SocketUDP *socket = (SocketUDP*) ss;
		char* packet = socket->getPacket();

		int numberRequest = getInt(packet, 0);
		std::vector<REQUEST> requests;

		int curOffset = 4;

		for (int i = 0; i < numberRequest; i++)
		{
			int minH = getInt(packet, curOffset);
			int maxH = getInt(packet, curOffset + 4);
			curOffset += 8;

			REQUEST r;
			r.minH = minH;
			r.maxH = maxH;
			r.posX = -1;
			r.posY = -1;

			requests.push_back(r);
		}

		int size = getInt(packet, curOffset);
		curOffset += 4;

		int n = size / 2 - 1;

		// Image processing
		for (int i = 0; i < size; i += 4)
		{
            char b = packet[curOffset + i];
            char g = packet[curOffset + i + 1];
            char r = packet[curOffset + i + 2];

            float h, s, v;

            RGBtoHSV((float)r, (float)g, (float)b, h, s, v);

            for (std::vector<REQUEST>::iterator it = requests.begin(); it != requests.end(); it++)
            {
            	REQUEST r = *it;
            	if (h >= r.minH && h <= r.maxH)
            	{
            		r.posY = i / n;
            		r.posX = i % n;
            	}
            }
		}

		// PROTOCOL ANSWER
		// INT POSX
		// INT POSY
		// For each request
		char buffer[numberRequest * 8];
		int i = 0;

		for (std::vector<REQUEST>::iterator it = requests.begin(); it != requests.end(); it++)
		{
			REQUEST r = *it;

			writeInt(buffer, i++ * 4, r.posX);
			writeInt(buffer, i++ * 4, r.posY);
		}

		socket->send((void*) buffer, numberRequest * 8);
	});

	logger->write("Starting the server...");
	server->start();

	logger->write("Type something to close the server or CTRL + C");

	fflush(stdin);
	std::cin.get();

	server->stop();

	if (server)
		delete server;

	return 1;
}