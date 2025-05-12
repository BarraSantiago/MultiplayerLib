using System.Net;

namespace Network.Messages;

public class MessageTracker
{
    private const int MaxRetries = 5;
    private readonly Dictionary<MessageType, int> _messageCounters = new();
    private readonly Dictionary<IPEndPoint, Dictionary<(MessageType, int), PendingMessage>> _pendingMessages = new();

    public int GetNextMessageNumber(MessageType type)
    {
        if (!_messageCounters.ContainsKey(type)) _messageCounters[type] = 0;

        var number = _messageCounters[type];
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
            LastSentTime = Time.realtimeSinceStartup
        };
    }

    public void ConfirmMessage(IPEndPoint target, MessageType type, int number)
    {
        if (_pendingMessages.TryGetValue(target, out var messages)) messages.Remove((type, number));
    }

    public void UpdateMessageSentTime(IPEndPoint target, MessageType type, int number)
    {
        if (_pendingMessages.TryGetValue(target, out var messages) &&
            messages.TryGetValue((type, number), out var message))
            message.LastSentTime = Time.realtimeSinceStartup;
    }

    public Dictionary<IPEndPoint, List<PendingMessage>> GetPendingMessages()
    {
        var result =
            new Dictionary<IPEndPoint, List<PendingMessage>>();

        foreach (var endpointEntry in _pendingMessages) result[endpointEntry.Key] = endpointEntry.Value.Values.ToList();

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