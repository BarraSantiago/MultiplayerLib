using System.Net;
using MultiplayerLib.Utils;

namespace MultiplayerLib.Network.Messages;

public class MessageTracker
{
    private const int MaxRetries = 5;
    private readonly Dictionary<MessageType, int> _messageCounters = new();
    private readonly Dictionary<IPEndPoint, Dictionary<(MessageType, int), PendingMessage>> _pendingMessages = new();

    public int GetNextMessageNumber(MessageType type)
    {
        if (!_messageCounters.ContainsKey(type)) _messageCounters[type] = 0;

        int number = _messageCounters[type];
        _messageCounters[type]++;
        return number;
    }

    public void AddPendingMessage(byte[] data, IPEndPoint target, MessageType type, int number)
    {
        if (!_pendingMessages.ContainsKey(target))
            _pendingMessages[target] = new Dictionary<(MessageType, int), PendingMessage>();

        _pendingMessages[target][(type, number)] = new PendingMessage
        {
            Data = data,
            MessageType = type,
            MessageNumber = number,
            LastSentTime = Time.CurrentTime
        };
    }

    public void ConfirmMessage(IPEndPoint target, MessageType type, int number)
    {
        if (_pendingMessages.TryGetValue(target, out Dictionary<(MessageType, int), PendingMessage>? messages)) messages.Remove((type, number));
    }

    public void UpdateMessageSentTime(IPEndPoint target, MessageType type, int number)
    {
        if (_pendingMessages.TryGetValue(target, out Dictionary<(MessageType, int), PendingMessage>? messages) &&
            messages.TryGetValue((type, number), out PendingMessage? message))
            message.LastSentTime = Time.CurrentTime;
    }

    public Dictionary<IPEndPoint, List<PendingMessage>> GetPendingMessages()
    {
        Dictionary<IPEndPoint, List<PendingMessage>> result =
            new Dictionary<IPEndPoint, List<PendingMessage>>();

        foreach (KeyValuePair<IPEndPoint, Dictionary<(MessageType, int), PendingMessage>> endpointEntry in _pendingMessages) result[endpointEntry.Key] = endpointEntry.Value.Values.ToList();

        return result;
    }

    public class PendingMessage
    {
        public byte[] Data { get; set; }
        public MessageType MessageType { get; set; }
        public int MessageNumber { get; set; }
        public float LastSentTime { get; set; }
    }
}