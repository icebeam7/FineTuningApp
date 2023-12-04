namespace FineTuningApp.Models
{
    public class ChatCompletionResponse
    {
        public string id { get; set; }
        public Choice[] choices { get; set; }
    }

    public class Choice
    {
        public int index { get; set; }
        public Message message { get; set; }
    }
}
