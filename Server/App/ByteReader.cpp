#include "ByteReader.h"

using namespace Util;

ByteReader::ByteReader(char *data)
  : data(data),
  pointer(0)
{
  if( O32_HOST_ORDER == O32_LITTLE_ENDIAN )
  {
    endian = little;
  }
  else if( O32_HOST_ORDER == O32_BIG_ENDIAN )
  {
    endian = big;
  }
  else
  {
    endian = native;
  }
}

ByteReader::~ByteReader()
{

}

void ByteReader::SetEndian(ByteEndianness end)
{
  endian = end;
}

long unsigned int ByteReader::Tell()
{
  return pointer;
}

void ByteReader::Seek(long unsigned int target)
{
  pointer = target;
}

char ByteReader::ReadByte()
{
  return ReadAny<char>();
}

float ByteReader::ReadSingle()
{
  return ReadAny<float>();
}

double ByteReader::ReadDouble()
{
  return ReadAny<double>();
}

short ByteReader::ReadInt16()
{
  return ReadAny<short>();
}

unsigned short ByteReader::ReadUInt16()
{
  return ReadAny<unsigned short>();
}

int ByteReader::ReadInt32()
{
  return ReadAny<int>();
}

unsigned int ByteReader::ReadUInt32()
{
  return ReadAny<unsigned int>();
}

long long int ByteReader::ReadInt64()
{
  return ReadAny<long long int>();
}

long long unsigned int ByteReader::ReadUInt64()
{
  return ReadAny<long long unsigned int>();
}

template<class T> T ByteReader::ReadAny()
{
  T ret;
  char *dst = (char *)&ret;
  char *src = (char *)&(data[pointer]);
  StoreBytes(src,dst,sizeof(T));
  pointer += sizeof(T);
  return ret;
}

void ByteReader::StoreBytes(
  char *src,
  char *dst,
  size_t size
)
{
  for( size_t i = 0; i < size; i++ )
  {
    if( O32_HOST_ORDER == O32_LITTLE_ENDIAN )
    {
      if( endian == little )
        dst[i] = src[i];
      else if( endian == big )
        dst[i] = src[(size-i-1)];
      else if( endian == native )
        dst[i] = src[(i%2==0?(size-i-2):(size-i))];
    }
    else if( O32_HOST_ORDER == O32_BIG_ENDIAN )
    {
      if( endian == big )
        dst[i] = src[i];
      else if( endian == little )
        dst[i] = src[(size-i-1)];
      else if( endian == native )
        dst[i] = src[(i%2==0?(i+1):(i-1))];
    }
    else
    {
      if( endian == native )
        dst[i] = src[i];
      else if( endian == little )
        dst[i] = src[(i%2==0?(size-i-2):(size-i))];
      else if( endian == big )
        dst[i] = src[(i%2==0?(i+1):(i-1))];
    }
  }
}