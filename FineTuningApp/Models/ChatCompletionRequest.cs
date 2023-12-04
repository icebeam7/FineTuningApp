namespace FineTuningApp.Models
{
    public class ChatCompletionRequest
    {
        public IEnumerable<Message> messages { get; set; }
    }
}
