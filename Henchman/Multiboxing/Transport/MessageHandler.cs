using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Henchman.Multiboxing.Command;

namespace Henchman.Multiboxing.Transport;

public static class MessageHandler
{
    internal static int receivedMessages;
    internal static int sentMessages;

    public static async Task<(CommandType header, string json)?> ReadMessageAsync(
            Stream            stream,
            CancellationToken token)
    {
        var headerBuffer    = new byte[2];
        var lengthBuffer    = new byte[4];
        var timestampBuffer = new byte[8];

        try
        {
            VerboseSpecific("MessageReader", "Starting ReadMessageAsync");

            await ReadExactAsync(stream, timestampBuffer, token);
            var timestamp = BitConverter.ToInt64(timestampBuffer, 0);
            var timestampReadable = DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                                                  .ToLocalTime();
            VerboseSpecific("MessageReader", $"RECEIVED MESSAGE CREATED AT: {timestampReadable:yyyy-MM-dd HH:mm:ss.fff}");

            await ReadExactAsync(stream, headerBuffer, token);
            var commandType = BitConverter.ToUInt16(headerBuffer, 0);
            VerboseSpecific("MessageReader", $"Header read: {(CommandType)commandType} ({commandType})");

            await ReadExactAsync(stream, lengthBuffer, token);
            var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            VerboseSpecific("MessageReader", $"Payload length read: {payloadLength} bytes");

            if (payloadLength < 0 || payloadLength > 10_000_000)
            {
                Verbose("Invalid payload length detected. Aborting.");
                return null;
            }

            var payloadBuffer = new byte[payloadLength];
            await ReadExactAsync(stream, payloadBuffer, token);
            VerboseSpecific("MessageReader", "Payload read complete");

            var json = Encoding.UTF8.GetString(payloadBuffer);
            VerboseSpecific("MessageReader", $"Decoded JSON: {json}");

            receivedMessages++;
            VerboseSpecific("MessageReader",
                            $"Received message ({receivedMessages}) - Type: {(CommandType)commandType} | Data: {json}");

            return ((CommandType)commandType, json);
        }
        catch (OperationCanceledException)
        {
            VerboseSpecific("MessageReader", $"Read canceled. Token state: {token.IsCancellationRequested}");
            return null;
        }
        catch (Exception ex)
        {
            VerboseSpecific("MessageReader", $"ReadMessageAsync failed: {ex}");
            return null;
        }
    }

    public static async Task WriteMessageAsync(
            Stream            stream,
            CommandType       commandType,
            string            json,
            CancellationToken token)
    {
        try
        {
            VerboseSpecific("MessageWriter", "Starting WriteMessageAsync");

            var timestamp      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timestampBytes = BitConverter.GetBytes(timestamp);

            var headerBytes = BitConverter.GetBytes((ushort)commandType);
            var jsonBytes   = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

            var totalLength = timestampBytes.Length +
                              headerBytes.Length    +
                              lengthBytes.Length    +
                              jsonBytes.Length;

            var messageBytes = new byte[totalLength];

            Buffer.BlockCopy(timestampBytes, 0, messageBytes, 0, timestampBytes.Length);
            Buffer.BlockCopy(headerBytes, 0, messageBytes, timestampBytes.Length, headerBytes.Length);
            Buffer.BlockCopy(lengthBytes, 0, messageBytes, timestampBytes.Length + headerBytes.Length, lengthBytes.Length);
            Buffer.BlockCopy(jsonBytes, 0, messageBytes,
                             timestampBytes.Length + headerBytes.Length + lengthBytes.Length,
                             jsonBytes.Length);

            VerboseSpecific("MessageWriter", $"Total message size: {totalLength} bytes");

            await stream.WriteAsync(messageBytes, 0, totalLength, token);
            await stream.FlushAsync(token);

            sentMessages++;
            VerboseSpecific("MessageWriter",
                            $"Sent message ({sentMessages}) - Type: {commandType} | Data: {json}");
        }
        catch (OperationCanceledException)
        {
            VerboseSpecific("MessageWriter", "WriteMessageAsync canceled.");
        }
        catch (IOException ioEx)
        {
            VerboseSpecific("MessageWriter", $"Stream I/O error: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            VerboseSpecific("MessageWriter", $"Unexpected error in WriteMessageAsync: {ex}");
        }
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset    = 0;
        var remaining = buffer.Length;

        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer, offset, remaining, token);
            if (read == 0)
                throw new IOException("Stream closed during read");

            offset    += read;
            remaining -= read;
        }
    }
}
