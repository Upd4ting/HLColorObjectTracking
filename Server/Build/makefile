LIB = ../CLib/CLib
APP = ../App

GPP = g++ -lpthread -m64 -Wall -std=c++11 -I $(LIB)

OBJ_LIBRARY = Input.o CSVFile.o PropFile.o Menu.o Logger.o Socket.o Server.o Client.o SocketUDP.o SocketTCP.o ServerTCP.o ServerUDP.o PTServer.o ClientUDP.o ClientTCP.o

OBJ_SERVER = servermain.o ByteReader.o ByteWriter.o lodepng.o

.SILENT:

all: Server

Server: $(OBJ_LIBRARY) $(OBJ_SERVER)
	echo 'Building server...'
	$(GPP) -o Server $(OBJ_LIBRARY) $(OBJ_SERVER)

# Compilation OBJ DE LA LIB
Input.o: $(LIB)/Input.cpp $(LIB)/Input.h
	$(GPP) -c $(LIB)/Input.cpp

CSVFile.o: $(LIB)/CSVFile.cpp $(LIB)/CSVFile.h
	$(GPP) -c $(LIB)/CSVFile.cpp

PropFile.o: $(LIB)/PropFile.cpp $(LIB)/PropFile.h
	$(GPP) -c $(LIB)/PropFile.cpp

Menu.o: $(LIB)/Menu.cpp $(LIB)/Menu.h
	$(GPP) -c $(LIB)/Menu.cpp

Logger.o: $(LIB)/Logger.cpp $(LIB)/Logger.h
	$(GPP) -c $(LIB)/Logger.cpp

Socket.o: $(LIB)/Socket.cpp $(LIB)/Socket.h
	$(GPP) -c $(LIB)/Socket.cpp

Server.o: $(LIB)/Server.cpp $(LIB)/Server.h
	$(GPP) -c $(LIB)/Server.cpp

Client.o: $(LIB)/Client.cpp $(LIB)/Client.h
	$(GPP) -c $(LIB)/Client.cpp

PTServer.o: $(LIB)/PTServer.cpp $(LIB)/PTServer.h
	$(GPP) -c $(LIB)/PTServer.cpp

SocketTCP.o: $(LIB)/SocketTCP.cpp $(LIB)/SocketTCP.h
	$(GPP) -c $(LIB)/SocketTCP.cpp

SocketUDP.o: $(LIB)/SocketUDP.cpp $(LIB)/SocketUDP.h
	$(GPP) -c $(LIB)/SocketUDP.cpp

ServerUDP.o: $(LIB)/ServerUDP.cpp $(LIB)/ServerUDP.h
	$(GPP) -c $(LIB)/ServerUDP.cpp

ServerTCP.o: $(LIB)/ServerTCP.cpp $(LIB)/ServerTCP.h
	$(GPP) -c $(LIB)/ServerTCP.cpp

ClientUDP.o: $(LIB)/ClientUDP.cpp $(LIB)/ClientUDP.h
	$(GPP) -c $(LIB)/ClientUDP.cpp

ClientTCP.o: $(LIB)/ClientTCP.cpp $(LIB)/ClientTCP.h
	$(GPP) -c $(LIB)/ClientTCP.cpp

# Compilation OBJ SERVER
servermain.o: $(APP)/servermain.cpp
	$(GPP) -c $(APP)/servermain.cpp

ByteReader.o: $(APP)/ByteReader.cpp $(APP)/ByteReader.h
	$(GPP) -c $(APP)/ByteReader.cpp

ByteWriter.o: $(APP)/ByteWriter.cpp $(APP)/ByteWriter.h
	$(GPP) -c $(APP)/ByteWriter.cpp

lodepng.o: $(APP)/lodepng.cpp $(APP)/lodepng.h
	$(GPP) -c $(APP)/lodepng.cpp