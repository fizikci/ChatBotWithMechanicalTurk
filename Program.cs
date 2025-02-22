using Newtonsoft.Json;
using System.Net;
using System.Resources;
using System.Threading.Tasks;

namespace ChatBotWithMechanicalTurk
{
    internal class Program
    {

        private string _prompt = @"You are an Auto Dealer chatbot. Try to understand customer's requests and help them. 

Currently the only service we provide is test drive booking. 
You should be using a Mechanical Turk system to communicate with the back end. There are human agents behind this system. They are waiting for your requests to help you, so you can help customers.
Whenever you need you can send requests to the Mechanical Turk system after your message to user.

Guidelines:
1. Gather Information: Ask necessary questions to comprehend the user's issue.
2. Communicate Clearly: Provide clear and concise responses to the user.
3. Never make up facts: If you don't know something, ask for help from the Mechanical Turk system.
3. Request or Post Data: Use the Mechanical Turk system for this. Send JSON data after ""Mechanical Turk Request: "" phrase.
4. Today's date: " + DateTime.Now.ToString("D") + @"
5. Example chat:
    System: The user is looking at a Tesla Model 3. VIN: 5YJ3E1EA7KF337717.
    User: Can I try this car?
    Assistant: Sure. When are you available for a test drive?
    User: I am available tomorrow.
    Assistant: Great! Please select a time slot: 10:00 AM, 11:00 AM, 12:00 PM, 1:00 PM, 2:00 PM, 3:00 PM, 4:00 PM, 5:00 PM.
    User: 2 pm is good
    Assistant: What would you like to drink during the test drive? Water, coffee, tea, or soda?
    User: Black coffee please
    Assistant: Great choice! Please let me know your email address and phone number for confirmation.
    User: johndoe@gmail.com, 8181234567
    Assistant: Please hold on while I am scheduling your test drive. Mechanical Turk Request: {
        ""type"": ""schedule-test-drive"",
        ""data"": {
            ""VIN"": ""5J6RW2H55KL000000"",
            ""Date"": """ + DateTime.Now.AddDays(1).ToString("MM/dd/yyyy") + @" 14:00"",
            ""Beverage"": ""Black coffee"",
            ""Email"": ""johndoe@gmail.com"",
            ""Phone"": ""8181234567""
        }
    }
    System: Scheduled.
    Assistant: Your test drive has been scheduled. You will receive a confirmation email shortly. Is there anything else I can help you with?
    User: This car has self driving feature right?
    Assistant: Let me check that for you. Mechanical Turk Request: {
        ""type"": ""get-car-details"",
        ""data"": {
            ""VIN"": ""5J6RW2H55KL000000"",
            ""query"": ""self driving""
        }
    }
    System: true
    Assistant: Yes, the car has a self-driving feature. Is there anything else I can help you with?
    User: No thank you
    Assistant: You're welcome. Have a great day!
6. Your chat should be similar to this, but you can send any Mechanical Turk Request where you see fit.
7. Users never see the Mechanical Turk requests and responses. You can receive a positive response or an error message from the Mechanical Turk system. So you need to explain it to the user and proceed from there.
8. If you receive an error, request alternative data or actions from the user and try again.
9. After providing help to the user, try to guess the next step. Instead of saying ""Is there anything else I can help you with?"", you can say ""Would you like to book a test drive?"" for example if it makes sense at that point. Be creative and helpful.";


        private List<GPTChatMessage> messages = new List<GPTChatMessage>();

        private List<GPTChatMessage> getFullChat()
        {
            var prompt = /*Properties.Resources.Prompt*/ _prompt
                .Replace("{TODAYS_DATE}", DateTime.Now.ToString("D"))
                .Replace("{TOMORROWS_DATE}", DateTime.Now.AddDays(1).ToString("D"));
            var list = new List<GPTChatMessage> { new GPTChatMessage { role = "system", content = prompt } };
            list.Add(new GPTChatMessage { role = "system", content = "The user is looking at a Tesla Model S. VIN: 5YJ3E1EA7KF338434." });
            list.AddRange(messages);

            return list;
        }

        private async Task<string> mechanicalTurk(string json)
        {
            writeLine("Mechanical Turk Request: ", ConsoleColor.Red, json);
            write("Mechanical Turk Response: ", ConsoleColor.Red);
            return await MessageQueueClient.SendRequestAsync(json);
        }

        private void write(string msg, ConsoleColor color = ConsoleColor.Gray)
        {
            Console.ForegroundColor = color;
            Console.Write(msg);
            Console.ResetColor();
        }
        private void writeLine(string msg, ConsoleColor color = ConsoleColor.Gray, string msg2 = null)
        {
            write(msg, color);
            Console.WriteLine(msg2);
        }

        public void Run()
        {
            writeLine("Welcome to the Mechanical Turk powered Virtual Assistant chat!");
            writeLine("Try to break it please!");
            writeLine("Type 'exit' to quit the chat");
            writeLine("");

            while (true)
            {
                write("You: ", ConsoleColor.Yellow);
                var userMessage = Console.ReadLine();
                if (string.IsNullOrEmpty(userMessage) || userMessage.ToLower() == "exit" || userMessage.ToLower() == "quit")
                {
                    writeLine("\n\nGoodbye!");
                    break;
                }

                messages.Add(new GPTChatMessage { role = "user", content = userMessage });

                getAssistantMessage();
            }
        }

        private async void getAssistantMessage()
        {
            var assistant = GPTChatCompletion.Submit(getFullChat());
            var parts = assistant.content.Split("Mechanical Turk Request:");
            var code = parts.Length > 1 ? parts[1].Trim() : null;
            var msgTxt = parts[0].Trim();

            messages.Add(new GPTChatMessage { role = "assistant", content = assistant.content });

            writeLine("Assistant: ", ConsoleColor.Green, msgTxt);

            if (!string.IsNullOrEmpty(code))
            {
                var codeReturn = await mechanicalTurk(code);

                messages.Add(new GPTChatMessage { role = "system", content = codeReturn });

                getAssistantMessage();

            }
        }

        static void Main(string[] args)
        {
            new Program().Run();
        }
    }

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

    public class GPTChatCompletion
    {
        public static GPTChatMessage Submit(List<GPTChatMessage> messages, string model = "gpt-4o", double temperature = 0.7, int max_tokens = 1000)
        {
            using (var wc = new WebClient())
            {
                var auth = "Bearer " + Environment.GetEnvironmentVariable("OPENAI_API_KEY");

                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                wc.Headers[HttpRequestHeader.Authorization] = auth;
                var resHttp = wc.UploadString("https://api.openai.com/v1/chat/completions",
                    JsonConvert.SerializeObject(new
                    {
                        messages,
                        model, // gpt-4 or gpt-3.5-turbo
                        temperature, // 0.0 to 1.0, 0.0 being the most predictable and 1.0 being the most surprising
                        max_tokens,
                        top_p = 1,
                        frequency_penalty = 0,
                        presence_penalty = 0
                    }));

                var resObj = JsonConvert.DeserializeObject<GPTChatCompletionResponse>(resHttp);

                return resObj?.choices?[0]?.message;
            }
        }

        public class Choice
        {
            public string text { get; set; }
            public int index { get; set; }
            public object logprobs { get; set; }
            public string finish_reason { get; set; }
        }

        public class APIResponse
        {
            public string id { get; set; }
            public string @object { get; set; }
            public int created { get; set; }
            public string model { get; set; }
            public List<Choice> choices { get; set; }
            public Usage usage { get; set; }
        }

        public class Usage
        {
            public int prompt_tokens { get; set; }
            public int completion_tokens { get; set; }
            public int total_tokens { get; set; }
        }

    }

    public class GPTChatMessage
    {
        public string role { get; set; } // system, assistant, user
        public string content { get; set; }
    }
    public class GPTChatChoice
    {
        public GPTChatMessage message { get; set; }
    }

    public class GPTChatCompletionResponse
    {
        public List<GPTChatChoice> choices { get; set; }
        public GPTChatError error { get; set; }
    }

    public class GPTChatError
    {
        public string message { get; set; }
    }

}
