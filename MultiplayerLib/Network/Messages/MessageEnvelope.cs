using System.Security.Cryptography;
using System.Text;

namespace MultiplayerLib.Network.Messages;

public class MessageEnvelope
{
    public bool IsCritical { get; set; }
    public MessageType MessageType { get; set; }
    public int MessageNumber { get; set; }
    public bool IsImportant { get; set; }
    public byte[] Data { get; set; }

    public int Checksum1 { get; private set; }
    public int Checksum2 { get; private set; }

    public byte[] Serialize()
    {
        List<byte> result = new List<byte>();
        result.Add(BitConverter.GetBytes(IsCritical ? 1 : 0)[0]);
        result.AddRange(BitConverter.GetBytes((int)MessageType));
        result.AddRange(BitConverter.GetBytes(MessageNumber));
        result.Add(BitConverter.GetBytes(IsImportant ? 1 : 0)[0]);

        if (Data != null)
        {
            byte[] dataToAdd = IsCritical ? EncryptData(Data) : Data;
            result.AddRange(dataToAdd);
        }

        CalculateChecksums();
        result.AddRange(BitConverter.GetBytes(Checksum1));
        result.AddRange(BitConverter.GetBytes(Checksum2));

        return result.ToArray();
    }

    public static MessageEnvelope Deserialize(byte[] data)
    {
        // Validate minimum message length (header + checksums)
        // 1 (critical) + 4 (msgType) + 4 (msgNum) + 1 (important) + 8 (checksums) = 18 bytes
        if (data == null) throw new ArgumentException("Data too short to be a valid message envelope");

        MessageEnvelope envelope = new MessageEnvelope();
        int offset = 0;

        envelope.IsCritical = data[offset] == 1;
        offset += 1;

        envelope.MessageType = (MessageType)BitConverter.ToInt32(data, offset);
        offset += 4;

        envelope.MessageNumber = BitConverter.ToInt32(data, offset);
        offset += 4;

        envelope.IsImportant = data[offset] == 1;
        offset += 1;

        // Calculate data length (everything except header and checksums)
        int dataLength = data.Length - offset - 8;

        // Handle message content (which could be null/empty)
        if (dataLength > 0)
        {
            byte[] messageData = new byte[dataLength];
            Array.Copy(data, offset, messageData, 0, dataLength);
            envelope.Data = envelope.IsCritical ? DecryptData(messageData) : messageData;
            offset += dataLength;
        }
        else
        {
            envelope.Data = null;
        }

        envelope.Checksum1 = BitConverter.ToInt32(data, offset);
        offset += 4;
        envelope.Checksum2 = BitConverter.ToInt32(data, offset);

        // Validate checksums
        int calculatedChecksum1, calculatedChecksum2;
        envelope.CalculateChecksums(out calculatedChecksum1, out calculatedChecksum2);

        if (calculatedChecksum1 != envelope.Checksum1 || calculatedChecksum2 != envelope.Checksum2)
            throw new Exception("Checksum verification failed");

        return envelope;
    }


    private void CalculateChecksums(out int checksum1, out int checksum2, int seed = 0)
    {
        int[][] operations = {
            new int[] { 0, 1, 2, 3, 4 }, 
            new int[] { 4, 3, 2, 1, 0 }, 
            new int[] { 2, 0, 3, 1, 4 }, 
            new int[] { 1, 4, 0, 2, 3 }, 
            new int[] { 3, 2, 4, 0, 1 } 
        };
        
        uint uChecksum1 = 0;
        uint uChecksum2 = 0x12345678;

        byte[] headerData = new byte[10];
        headerData[0] = (byte)(IsCritical ? 1 : 0);
        Array.Copy(BitConverter.GetBytes((int)MessageType), 0, headerData, 1, 4);
        Array.Copy(BitConverter.GetBytes(MessageNumber), 0, headerData, 5, 4);
        headerData[9] = (byte)(IsImportant ? 1 : 0);

        ProcessBytes(headerData, ref uChecksum1, ref uChecksum2, operations[seed % operations.Length], 0);
        if (Data != null)
        {
            ProcessBytes(Data, ref uChecksum1, ref uChecksum2, operations[seed % operations.Length], headerData.Length);
        }

        uChecksum1 = (uChecksum1 & 0xFFFF) + (uChecksum1 >> 16);
        uChecksum2 += uChecksum1;

        checksum1 = (int)uChecksum1;
        checksum2 = (int)uChecksum2;
    }

    private void ProcessBytes(byte[] data, ref uint uChecksum1, ref uint uChecksum2, int[] operations, int offset)
    {
        for (int i = 0; i < data.Length; i++)
        {
            int opIndex = (i + offset) % operations.Length;

            switch (operations[opIndex])
            {
                case 0:
                    uChecksum1 += data[i];
                    uChecksum2 += (uint)(data[i] << 3);
                    break;

                case 1:
                    uChecksum1 ^= (uint)(data[i] << ((i + offset) & 0x0F));
                    uChecksum2 ^= (uint)(data[i] << ((i + offset + 3) & 0x0F));
                    break;

                case 2:
                    uChecksum1 |= (uint)(data[i] << (i % 8));
                    uChecksum2 |= (uint)(data[i] << ((i + 5) % 8));
                    break;

                case 3: 
                    uChecksum1 &= 0xFFFFFFFF - (uint)data[i];
                    uChecksum2 &= 0xFFFFFFFF - (uint)(data[i] << 2);
                    break;

                case 4:
                    uChecksum1 = (uChecksum1 << 3) | (uChecksum1 >> 29);
                    uChecksum2 = (uChecksum2 << 5) | (uChecksum2 >> 27);
                    uChecksum1 += data[i];
                    uChecksum2 ^= data[i];
                    break;
            }
        }
    }

    private void CalculateChecksums()
    {
        CalculateChecksums(out int checksum1, out int checksum2, 0);
        Checksum1 = checksum1;
        Checksum2 = checksum2;
    }

    private byte[] EncryptData(byte[] data)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] key = sha256.ComputeHash(Encoding.UTF8.GetBytes("SecretKey"));

        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using MemoryStream ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

        using CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    private static byte[] DecryptData(byte[] encryptedData)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] key = sha256.ComputeHash(Encoding.UTF8.GetBytes("SecretKey"));

        using Aes aes = Aes.Create();
        aes.Key = key;

        byte[] iv = new byte[16]; // AES block size
        Array.Copy(encryptedData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using MemoryStream ms = new MemoryStream();
        using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
        cs.Write(encryptedData, iv.Length, encryptedData.Length - iv.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }
}