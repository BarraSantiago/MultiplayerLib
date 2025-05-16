namespace MultiplayerLib.Network.Messages;

public class MessageSequenceTracker
{
    // Maps: ClientId -> MessageType -> Last Message Number
    private Dictionary<int, Dictionary<MessageType, int>> _lastMessageNumbers = new();
    
    // Stores out-of-order messages: ClientId -> MessageType -> MessageNumber -> Data
    private Dictionary<int, Dictionary<MessageType, SortedDictionary<int, byte[]>>> _pendingMessages = new();
    
    public void InitializeClient(int clientId)
    {
        if (!_lastMessageNumbers.ContainsKey(clientId))
        {
            _lastMessageNumbers[clientId] = new Dictionary<MessageType, int>();
            _pendingMessages[clientId] = new Dictionary<MessageType, SortedDictionary<int, byte[]>>();
        }
    }
    
    public void RemoveClient(int clientId)
    {
        _lastMessageNumbers.Remove(clientId);
        _pendingMessages.Remove(clientId);
    }
    
    public bool CheckMessageSequence(int clientId, MessageType messageType, int messageNumber, byte[] data, out List<byte[]> messagesToProcess)
    {
        messagesToProcess = new List<byte[]>();
        
        if (!_lastMessageNumbers.ContainsKey(clientId))
            InitializeClient(clientId);
        
        if (!_lastMessageNumbers[clientId].ContainsKey(messageType))
        {
            _lastMessageNumbers[clientId][messageType] = messageNumber - 1;
            if (!_pendingMessages[clientId].ContainsKey(messageType))
                _pendingMessages[clientId][messageType] = new SortedDictionary<int, byte[]>();
        }
        
        int expectedNumber = _lastMessageNumbers[clientId][messageType] + 1;
        
        // Message is in sequence
        if (messageNumber == expectedNumber)
        {
            _lastMessageNumbers[clientId][messageType] = messageNumber;
            messagesToProcess.Add(data);
            
            // Process any pending messages that can now be handled
            ProcessPendingMessages(clientId, messageType, messagesToProcess);
            return true;
        }
        
        // Already processed this message
        if (messageNumber <= _lastMessageNumbers[clientId][messageType])
            return true;
        
        // We have a gap - store message for later
        _pendingMessages[clientId][messageType][messageNumber] = data;
        return false;
    }
    
    private void ProcessPendingMessages(int clientId, MessageType messageType, List<byte[]> messagesToProcess)
    {
        if (!_pendingMessages[clientId].ContainsKey(messageType) || 
            _pendingMessages[clientId][messageType].Count == 0)
            return;
        
        bool foundNext = true;
        while (foundNext)
        {
            int expectedNext = _lastMessageNumbers[clientId][messageType] + 1;
            foundNext = false;
            
            if (_pendingMessages[clientId][messageType].TryGetValue(expectedNext, out byte[] pendingData))
            {
                _lastMessageNumbers[clientId][messageType] = expectedNext;
                _pendingMessages[clientId][messageType].Remove(expectedNext);
                messagesToProcess.Add(pendingData);
                foundNext = true;
            }
        }
    }
    
    public List<int> GetMissingMessageNumbers(int clientId, MessageType messageType)
    {
        List<int> missingNumbers = new List<int>();
        
        if (!_lastMessageNumbers.ContainsKey(clientId) || 
            !_lastMessageNumbers[clientId].ContainsKey(messageType) ||
            !_pendingMessages[clientId].ContainsKey(messageType) ||
            _pendingMessages[clientId][messageType].Count == 0)
            return missingNumbers;
        
        int lastReceived = _lastMessageNumbers[clientId][messageType];
        int firstPending = _pendingMessages[clientId][messageType].Keys.Min();
        
        // Find gaps between last received and pending messages
        for (int i = lastReceived + 1; i < firstPending; i++)
            missingNumbers.Add(i);
        
        // Find gaps within pending messages
        int previousKey = firstPending;
        foreach (int key in _pendingMessages[clientId][messageType].Keys.Skip(1))
        {
            for (int i = previousKey + 1; i < key; i++)
                missingNumbers.Add(i);
            previousKey = key;
        }
        
        return missingNumbers;
    }
}