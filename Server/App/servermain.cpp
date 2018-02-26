#include <iostream>
#include <signal.h>
#include <algorithm>
#include <cstdlib>
#include <cmath>
#include <sstream>
#include <ctime>
#include "PTServer.h"
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

typedef struct Params {
	char *packet;
	int start;
	int size;
	int width;
	std::vector<REQUEST> *requests;
} PARAMS;

PTServer *server = NULL;
Logger *logger = NULL;

// Var for threads
pthread_mutex_t mutex;

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

// Thread function
void* threadProcessData(void *p)
{
	PARAMS *param = (PARAMS*) p;
	for (int i = 0; i < param->size; i += 16)
	{
	    char b = param->packet[param->start + i];
	    char g = param->packet[param->start + i + 1];
	    char r = param->packet[param->start + i + 2];

	    float h, s, v;

	    RGBtoHSV((float)r, (float)g, (float)b, h, s, v);

	    for (REQUEST &r : *(param->requests))
	    {
	    	if (h >= r.minH && h <= r.maxH)
	    	{
	    		pthread_mutex_lock(&mutex);
	    		if (r.posX == -1 && r.posY == -1)
	    		{
		    		r.posY = i / param->width;
	    			r.posX = i % param->width;
	    		}
	    		pthread_mutex_unlock(&mutex);
	    	}
	    }
	}

	free(param);
	return NULL;
}

int getInt(char *buffer, int start)
{
	unsigned int num = ((u8)buffer[start] << 24) | ((u8)buffer[start + 1] << 16) | ((u8)buffer[start + 2] << 8) | ((u8)buffer[start + 3]);
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

	pthread_mutex_init(&mutex, NULL);

	PropFile prop("server.conf");
	logger = new Logger("server.log");

	server = PTServer::create(prop["ip"], convertInt(prop["port"]), convertInt(prop["threads"]), logger);

	if (server == NULL)
	{
		logger->write("Error when creating the server!");
		return 1;
	}

	// Calculating number of workers
	int numCPU = sysconf(_SC_NPROCESSORS_ONLN);
	int NW = 4;

	while (NW < numCPU && NW <= 32)
	{
		NW *= 2;
	}

	std::cout << "Number of worker: " << NW << std::endl;

	// PROTOCOL
	// INT -> Number of requests
	// 	INT -> minH
	// 	INT -> maxH
	//  ... 
	// INT -> Width of the picture
	// INT -> Size of the picture
	// BYTES -> IMAGE

	server->addListener([&](SocketTCP *s) {
		s->setMessageListener([&](Socket *ss) {
			SocketTCP *socket = (SocketTCP*) ss;
			char packet[2 * 1048576];

			socket->read(packet);

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

			int width = getInt(packet, curOffset);curOffset += 4;
			int size = getInt(packet, curOffset);curOffset += 4;

			clock_t begin = clock();

			// Starting pool of thread for image processing
			std::vector<pthread_t> workers;
			int slice = size / NW;

			for (int i = 0; i < NW; i++) 
			{
				PARAMS *param = (PARAMS*) malloc(sizeof(PARAMS));
				param->packet = packet;
				param->start = curOffset + slice * i;
				param->size = slice;
				param->width = width;
				param->requests = &requests;

				pthread_t thread;
				pthread_create(&thread, NULL, threadProcessData, param);
				workers.push_back(thread);
			}

			for (pthread_t &t : workers)
			{
				pthread_join(t, NULL);
			}

			clock_t end = clock();
			double elapsed_secs = double(end - begin) / CLOCKS_PER_SEC;
			std::cout << "Elapsed sec for image processing: " << elapsed_secs << std::endl;

			// PROTOCOL ANSWER
			// Num request
			// INT POSX
			// INT POSY
			// For each request
			char buffer[numberRequest * 8 + 4];

			int i = 0;

			writeInt(buffer, i++, numberRequest);

			for (REQUEST &r : requests)
			{
				writeInt(buffer, i++ * 4, r.posX);
				writeInt(buffer, i++ * 4, r.posY);
			}

			socket->write((void*) buffer, numberRequest * 8 + 4);
			std::cout << "Response sent" << std::endl;
		});
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