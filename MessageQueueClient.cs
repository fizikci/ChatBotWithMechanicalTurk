using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ChatBotWithMechanicalTurk
{
    public class MessageQueueClient
    {
        private static readonly string QueueFilePath = "messageQueue.txt";
        private static readonly object FileLock = new object();

        public static async Task<string> SendRequestAsync(string request)
        {
            await Task.Run(() => EnqueueMessage(request));
            return await Task.Run(() => DequeueMessage());
        }

        private static void EnqueueMessage(string message)
        {
            lock (FileLock)
            {
                using (var writer = new StreamWriter(QueueFilePath, true))
                {
                    writer.WriteLine(message);
                }
            }
        }

        private static string DequeueMessage()
        {
            lock (FileLock)
            {
                if (!File.Exists(QueueFilePath))
                {
                    return null;
                }

                var messages = new List<string>(File.ReadAllLines(QueueFilePath));
                if (messages.Count == 0)
                {
                    return null;
                }

                var message = messages[0];
                messages.RemoveAt(0);
                File.WriteAllLines(QueueFilePath, messages.ToArray());

                return message;
            }
        }
    }
}
