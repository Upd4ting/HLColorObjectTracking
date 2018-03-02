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
#include "ByteReader.h"
#include "ByteWriter.h"
#include "RgbHsv.h"

using namespace std;
using namespace Util;

typedef unsigned char u8;

typedef struct Request {
	int minH;
	int maxH;
	int minLight;
	int minSaturation;
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

// Thread function
void* threadProcessData(void *p)
{
	PARAMS *param = (PARAMS*) p;
	for (int i = 0; i < param->size; i += 4)
	{
	    unsigned char b = param->packet[param->start + i];
	    unsigned char g = param->packet[param->start + i + 1];
	    unsigned char r = param->packet[param->start + i + 2];

	    RGB data = RGB(r, g, b);
	    HSV value = RGBToHSV(data);

	    for (REQUEST &r : *(param->requests))
	    {
	    	if (value.H >= r.minH && value.H <= r.maxH && value.S >= r.minSaturation && value.V >= r.minLight)
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
		return 1
;	}

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
	//  INT -> minSaturation
	//  INT -> minLight
	//  ... 
	// FLOAT (4 byte) -> TimeStamp
	// INT -> Width of the picture
	// INT -> Height of the picture
	// INT -> Size of the picture
	// BYTES -> IMAGE

	server->addListener([&](SocketTCP *s) {
		s->setMessageListener([&](Socket *ss) {
			SocketTCP *socket = (SocketTCP*) ss;
			char packet[2 * 1048576];

			socket->read(packet);

			clock_t totalBegin = clock();

			ByteReader reader(packet);
			reader.SetEndian(big);

			int numberRequest = reader.ReadInt32();

			std::vector<REQUEST> requests;

			for (int i = 0; i < numberRequest; i++)
			{
				REQUEST r;
				r.minH = reader.ReadInt32();
				r.maxH = reader.ReadInt32();
				r.minSaturation = reader.ReadInt32();
				r.minLight = reader.ReadInt32();
				r.posX = -1;
				r.posY = -1;

				requests.push_back(r);
			}
			long long int timestamp = reader.ReadInt64();
			int width = reader.ReadInt32();
			int size = reader.ReadInt32();

			clock_t begin = clock();

			// Starting pool of thread for image processing
			std::vector<pthread_t> workers;
			int slice = size / NW;

			for (int i = 0; i < NW; i++) 
			{
				PARAMS *param = (PARAMS*) malloc(sizeof(PARAMS));
				param->packet = packet;
				param->start = reader.Tell() + slice * i;
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
			// INT Number request
			// LONG Timestamp
			// INT POSX
			// INT POSY
			// For each request
			ByteWriter writer(1024);
			writer.SetEndian(little);

			writer.WriteInt32(numberRequest);
			writer.WriteInt64(timestamp);

			for (REQUEST &r : requests)
			{
				writer.WriteInt32(r.posX);
				writer.WriteInt32(r.posY);
			}

			socket->write((void*) writer.GetData(), writer.Tell());

			clock_t totalEnd = clock();
			double elapsed_secs2 = double(totalEnd - totalBegin) / CLOCKS_PER_SEC;
			std::cout << "Response sent in " << elapsed_secs2 << std::endl;
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