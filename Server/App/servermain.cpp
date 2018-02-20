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
	logger->write("Fermeture du serveur...");

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
		logger->write("Erreur création de la classe server !");
		return 1;
	}

	// PROTOCOL
	// INT -> minH
	// INT -> maxH
	// INT -> Taille de l'image
	// BYTES -> IMAGE

	server->getSocket()->setMessageListener([&](Socket *ss) {
		SocketUDP *socket = (SocketUDP*) ss;
		char* packet = socket->getPacket();

		int minH = getInt(packet, 0);
		int maxH = getInt(packet, 4);
		int size = getInt(packet, 8);

		int n = size / 2 - 1;

		int posX = 0, posY = 0;

		// Traitement de l'image
		for (int i = 0; i < size; i += 4)
		{
            char b = packet[12 + i];
            char g = packet[12 + i + 1];
            char r = packet[12 + i + 2];

            float h, s, v;

            RGBtoHSV((float)r, (float)g, (float)b, h, s, v);

            if (h >= minH && h <= maxH)
            {
            	posY = i / n;
            	posX = i % n;
            	break;
            }
		}

		// PROTOCOL ANSWER
		// INT POSX
		// INT POSY
		char buffer[8];
		writeInt(buffer, 0, posX);
		writeInt(buffer, 4, posY);

		socket->send((void*) buffer, 8);
	});

	logger->write("Start du serveur...");
	server->start();

	logger->write("Appuyer sur une touche pour fermer le serveur !");

	fflush(stdin);
	std::cin.get();

	server->stop();

	if (server)
		delete server;

	return 1;
}