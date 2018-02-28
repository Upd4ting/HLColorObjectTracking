#ifndef __BYTE_READER__
#define __BYTE_READER__

#include <limits.h>
#include <stdint.h>
#include <string>
#include <vector>

#if CHAR_BIT != 8
#error "unsupported char size"
#endif

namespace Util
{
  enum
  {
    O32_LITTLE_ENDIAN = 0x03020100ul,
    O32_BIG_ENDIAN = 0x00010203ul,
    O32_POP_ENDIAN = 001000302ul
  };

  static const union
  {
    unsigned char bytes[4];
    uint32_t value;
  }
  o32_host_order = { { 0, 1, 2, 3 } };
  #define O32_HOST_ORDER (o32_host_order.value)

  enum ByteEndianness
  {
    little,
    big,
    native
  };

  class ByteReader
  {
  public:
    ByteReader(char*);
    ~ByteReader();

    void SetEndian(ByteEndianness);

    long long unsigned int ReadUInt64();
    long long int ReadInt64();

    unsigned int ReadUInt32();
    int ReadInt32();

    unsigned short ReadUInt16();
    short ReadInt16();

    double ReadDouble();
    float ReadSingle();

    char ReadByte();
    std::vector<char> ReadBytes(int size);

    unsigned char ReadUnsignedByte();
    std::vector<unsigned char> ReadUnsignedBytes(int size);

    long unsigned int Tell();
    void Seek(long unsigned int);

  private:
    const char *data;
    ByteEndianness endian;

    void StoreBytes(char *, char *, size_t);
    template<class T> T ReadAny();

    long unsigned int pointer;
  };
}

#endif