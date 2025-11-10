using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Henchman.Multibox.Command;

namespace Henchman.Multibox
{
    public static class MessageHandler
    {
        internal static int receivedMessages = 0;
        internal static int sentMessages = 0;
        public static async Task<(CommandType header, string json)?> ReadMessageAsync(PipeStream pipe, CancellationToken token)
        {
            var headerBuffer = new byte[2];
            var lengthBuffer = new byte[4];

            try
            {
                VerboseSpecific("MessageReader", "Starting ReadMessageAsync");

                var timestampBuffer = new byte[8];
                await pipe.ReadExactlyAsync(timestampBuffer, 0, 8, token).ConfigureAwait(false);
                var timestamp         = BitConverter.ToInt64(timestampBuffer, 0);
                var timestampReadable = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).ToLocalTime();
                VerboseSpecific("MessageReader", $"RECEIVED MESSAGE CREATED AT: {timestampReadable:yyyy-MM-dd HH:mm:ss.fff}");

                await pipe.ReadExactlyAsync(headerBuffer, 0, 2, token).ConfigureAwait(false); ;
                var commandType = BitConverter.ToUInt16(headerBuffer, 0);
                VerboseSpecific("MessageReader", $"Header read: {(CommandType)commandType} ({commandType})");

                await pipe.ReadExactlyAsync(lengthBuffer, 0, 4, token).ConfigureAwait(false); ;
                var payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
                VerboseSpecific("MessageReader", $"Payload length read: {payloadLength} bytes");

                if (payloadLength < 0 || payloadLength > 10_000_000)
                {
                    Verbose("Invalid payload length detected. Aborting.");
                    return null;
                }

                var payloadBuffer = new byte[payloadLength];
                await pipe.ReadExactlyAsync(payloadBuffer, 0, payloadLength, token).ConfigureAwait(false); ;
                VerboseSpecific("MessageReader", "Payload read complete");

                var json = Encoding.UTF8.GetString(payloadBuffer);
                VerboseSpecific("MessageReader", $"Decoded JSON: {json}");
                receivedMessages++;
                VerboseSpecific("MessageReader", $"Received message ({receivedMessages}) - Type: {(CommandType)commandType} | Data: {json}");
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


        public static async Task WriteMessageAsync(PipeStream pipe, CommandType commandType, string json, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested)
                {
                    VerboseSpecific("MessageWriter","Cancellation requested before WriteAsync");
                }

                VerboseSpecific("MessageWriter",$"Pipe connected: {pipe.IsConnected}");
                VerboseSpecific("MessageWriter", "Starting WriteMessageAsync");

                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var timestampBytes = BitConverter.GetBytes(timestamp);
                var headerBytes = BitConverter.GetBytes((ushort)commandType);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

                VerboseSpecific("MessageWriter", $"Header prepared: {commandType} ({(ushort)commandType}) - {headerBytes.Length} bytes");
                VerboseSpecific("MessageWriter", $"Payload length: {jsonBytes.Length} bytes");
                VerboseSpecific("MessageWriter", $"Payload content: {json}");

                var totalLength  = timestampBytes.Length + headerBytes.Length + lengthBytes.Length + jsonBytes.Length;
                var messageBytes = new byte[totalLength];

                Buffer.BlockCopy(timestampBytes, 0, messageBytes, 0, timestampBytes.Length);
                Buffer.BlockCopy(headerBytes, 0, messageBytes, timestampBytes.Length, headerBytes.Length);
                Buffer.BlockCopy(lengthBytes, 0, messageBytes, timestampBytes.Length + headerBytes.Length, lengthBytes.Length);
                Buffer.BlockCopy(jsonBytes, 0, messageBytes, timestampBytes.Length   + headerBytes.Length + lengthBytes.Length, jsonBytes.Length);


                VerboseSpecific("MessageWriter", $"Total message size: {totalLength} bytes");
                VerboseSpecific("MessageWriter", $"Writing message ({sentMessages}) to pipe...");

                await pipe.WriteAsync(messageBytes, 0, totalLength, token).ConfigureAwait(false);
                VerboseSpecific("MessageWriter", "Message written");

                await pipe.FlushAsync(token).ConfigureAwait(false);
                VerboseSpecific("MessageWriter", "Pipe flushed");

                await Task.Run(() => pipe.WaitForPipeDrain(), token).ConfigureAwait(false);
                VerboseSpecific("MessageWriter", "Pipe drain complete");

                sentMessages++;
                VerboseSpecific("MessageWriter", $"Sent message ({sentMessages}) - Type: {commandType} | Data: {json}");
            }
            catch (OperationCanceledException)
            {
                VerboseSpecific("MessageWriter", "WriteMessageAsync canceled.");
            }
            catch (IOException ioEx)
            {
                VerboseSpecific("MessageWriter", $"Pipe I/O error: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                VerboseSpecific("MessageWriter", $"Unexpected error in WriteMessageAsync: {ex}");
            }
        }
    }
}
