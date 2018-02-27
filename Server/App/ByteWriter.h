#ifndef __BYTE_WRITER__
#define __BYTE_WRITER__

#include <limits.h>
#include <stdint.h>
#include <string>
#include "ByteReader.h"

namespace Util
{
  class ByteWriter
  {
  public:
    ByteWriter(int);
    ~ByteWriter();

    void SetEndian(ByteEndianness);

    void WriteUInt64(long long unsigned int value);
    void WriteInt64(long long int value);

    void WriteUInt32(unsigned int value);
    void WriteInt32(int value);

    void WriteUInt16(unsigned short value);
    void WriteInt16(short value);

    void WriteDouble(double value);
    void WriteSingle(float value);

    void WriteByte(char value);

    long unsigned int Tell();
    void Seek(long unsigned int);

    char* GetData();

  private:
    char *data;
    ByteEndianness endian;

    void StoreBytes(char *, char *, size_t);
    template<class T> void WriteAny(T value);

    long unsigned int pointer;
  };
}

#endif