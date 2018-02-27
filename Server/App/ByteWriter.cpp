#include "ByteWriter.h"

using namespace Util;

ByteWriter::ByteWriter(int size)
  : pointer(0)
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

  data = new char[size];
}

ByteWriter::~ByteWriter()
{
  delete[] data;
}

void ByteWriter::SetEndian(ByteEndianness end)
{
  endian = end;
}

long unsigned int ByteWriter::Tell()
{
  return pointer;
}

void ByteWriter::Seek(long unsigned int target)
{
  pointer = target;
}

char* ByteWriter::GetData()
{
  return data;
}

void ByteWriter::WriteByte(char value)
{
  WriteAny<char>(value);
}

void ByteWriter::WriteSingle(float value)
{
  WriteAny<float>(value);
}

void ByteWriter::WriteDouble(double value)
{
  WriteAny<double>(value);
}

void ByteWriter::WriteInt16(short value)
{
  WriteAny<short>(value);
}

void ByteWriter::WriteUInt16(unsigned short value)
{
  WriteAny<unsigned short>(value);
}

void ByteWriter::WriteInt32(int value)
{
  WriteAny<int>(value);
}

void ByteWriter::WriteUInt32(unsigned int value)
{
  WriteAny<unsigned int>(value);
}

void ByteWriter::WriteInt64(long long int value)
{
  WriteAny<long long int>(value);
}

void ByteWriter::WriteUInt64(long long unsigned int value)
{
  WriteAny<long long unsigned int>(value);
}

template<class T> void ByteWriter::WriteAny(T value)
{
  char *src = (char *)&value;
  char *dst = (char *)&(data[pointer]);
  StoreBytes(src,dst,sizeof(T));
  pointer += sizeof(T);
}

void ByteWriter::StoreBytes(
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