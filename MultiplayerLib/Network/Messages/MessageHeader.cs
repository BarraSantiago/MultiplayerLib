using System.Security.Cryptography;

namespace MultiplayerLib.Network.Messages;

public class MessageHeader
{
    public const int HEADER_SIZE = 24;

    public ushort HeaderChecksum { get; private set; }
    public uint BodyChecksum { get; private set; }

    public byte[] Serialize(MessageType type, uint sequenceNumber, bool isImportant)
    {
        byte[] header = new byte[HEADER_SIZE];

        BitConverter.GetBytes((int)type).CopyTo(header, 0);

        BitConverter.GetBytes(sequenceNumber).CopyTo(header, 4);

        header[8] = (byte)(isImportant ? 1 : 0);

        BitConverter.GetBytes((ushort)0).CopyTo(header, 9);
        BitConverter.GetBytes((uint)0).CopyTo(header, 11);

        HeaderChecksum = CalculateHeaderChecksum(header);
        BitConverter.GetBytes(HeaderChecksum).CopyTo(header, 9);

        return header;
    }

    public static (MessageType mType, uint mNum, bool isImportant, uint bodyCheckSum) Deserialize(byte[] data)
    {
        if (data.Length < HEADER_SIZE)
            throw new ArgumentException("Data too short for header");

        // Extract fields
        MessageType type = (MessageType)BitConverter.ToInt32(data, 0);
        uint sequenceNumber = BitConverter.ToUInt32(data, 4);
        bool isImportant = data[8] == 1;
        ushort storedHeaderChecksum = BitConverter.ToUInt16(data, 9);
        uint storedBodyChecksum = BitConverter.ToUInt32(data, 11);

        // Verify header checksum
        byte[] headerCopy = new byte[HEADER_SIZE];
        Array.Copy(data, headerCopy, HEADER_SIZE);
        BitConverter.GetBytes((ushort)0).CopyTo(headerCopy, 9);

        ushort calculatedHeaderChecksum = CalculateHeaderChecksum(headerCopy);
        if (calculatedHeaderChecksum != storedHeaderChecksum)
            throw new InvalidOperationException("Header checksum verification failed");

        return (type, sequenceNumber, isImportant, storedBodyChecksum);
    }

    public static ushort CalculateHeaderChecksum(byte[] header)
    {
        ushort sum1 = 0;
        ushort sum2 = 0;

        for (int i = 0; i < 9; i++)
        {
            sum1 = (ushort)((sum1 + header[i]) % 255);
            sum2 = (ushort)((sum2 + sum1) % 255);
        }

        return (ushort)((sum2 << 8) | sum1);
    }

    public static uint CalculateBodyChecksum(byte[] body)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(body);
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}